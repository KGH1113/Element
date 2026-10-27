// Windows host for the in-game ImplDmNote viewers.
//
// Each OBS page renders through a WebView2 composition controller into a
// DirectComposition visual. The visual tree is bound to the Unity window as a
// topmost target, so it composites above the game's swap chain inside the game
// window itself: window capture includes it, and no extra window exists. No
// input is forwarded to WebView2, so clicks and keys reach Unity unchanged.
//
// Everything runs on the Unity window's thread, a COM single-threaded apartment
// whose message pump also delivers WebView2 callbacks and this host's timers.

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>

#include <d2d1_1.h>
#include <d3d11.h>
#include <dcomp.h>
#include <dxgi.h>
#include <knownfolders.h>
#include <shlobj.h>
#include <wrl.h>

#include "WebView2.h"

#include "GameWebView.h"
#include "ViewerScripts.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <deque>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <vector>

namespace {
using Microsoft::WRL::Callback;
using Microsoft::WRL::ComPtr;

constexpr double kInset = 16;
constexpr double kMaximumWidth = 430;
constexpr double kMaximumHeight = 270;
constexpr int kMaximumViewport = 4096;
constexpr double kMinimumVisibleFraction = 0.1;
constexpr UINT_PTR kRecoverTimer = 1;
constexpr UINT_PTR kIdleTimer = 2;
constexpr UINT_PTR kTeardownTimer = 3;
constexpr UINT kRecoverDelayMs = 1000;
// Shared WebView2/D3D resources outlive a host briefly: gameplay sessions end and
// start again on pause, retry and level changes, and a warm browser process
// makes the next attachment immediate.
constexpr UINT kIdleReleaseMs = 60000;
constexpr int kMaximumRecoveries = 5;
constexpr auto kRecoveryWindow = std::chrono::seconds(60);

// Host errors describe the latest call or the shared WebView2 environment.
// Faults belong to one viewer, so one page loading never clears another's.
std::mutex errorMutex;
std::string hostError;
std::map<std::string, std::string> faults;
std::atomic<unsigned long long> geometryRevision{1};

// Deliberately never include HRESULT text or URLs: the OBS viewer authenticates
// through a query token, which must not enter game logs.
void SetError(const char *message) {
  std::lock_guard<std::mutex> lock(errorMutex);
  hostError = message;
}

void SetFault(const std::string &kind, const char *message) {
  std::lock_guard<std::mutex> lock(errorMutex);
  if (message == nullptr || message[0] == '\0') faults.erase(kind);
  else faults[kind] = message;
}

void ClearFaults() {
  std::lock_guard<std::mutex> lock(errorMutex);
  faults.clear();
}

// Managed code maps these phrases to player-facing statuses (NativeViewerErrors).
constexpr const char *kRendererStopped = "Native viewer renderer stopped. Viewers will reconnect automatically.";
constexpr const char *kLoadRetrying =
    "Native viewer could not load its local source. Viewers will reconnect automatically.";
constexpr const char *kLoadFailed = "Native viewer could not load its local source. Check ImplDmNote.";
constexpr const char *kRuntimeTooOld =
    "This WebView2 Runtime is too old for the in-game viewer. Update it and restart ADOFAI.";

struct RectD {
  double x = 0, y = 0, width = 0, height = 0;
  double Right() const { return x + width; }
  double Bottom() const { return y + height; }
  bool Empty() const { return !(width > 0 && height > 0); }
};

bool SameRect(const RectD &left, const RectD &right) {
  return left.x == right.x && left.y == right.y && left.width == right.width &&
         left.height == right.height;
}

RectD Intersect(const RectD &left, const RectD &right) {
  double l = std::max(left.x, right.x), t = std::max(left.y, right.y);
  double r = std::min(left.Right(), right.Right()), b = std::min(left.Bottom(), right.Bottom());
  if (r <= l || b <= t) return {};
  return {l, t, r - l, b - t};
}

RectD Inflate(const RectD &rect, double amount) {
  return {rect.x - amount, rect.y - amount, rect.width + amount * 2, rect.height + amount * 2};
}

std::wstring Widen(const std::string &text) {
  if (text.empty()) return {};
  int length = MultiByteToWideChar(CP_UTF8, 0, text.data(), (int)text.size(), nullptr, 0);
  std::wstring result(length, L'\0');
  MultiByteToWideChar(CP_UTF8, 0, text.data(), (int)text.size(), result.data(), length);
  return result;
}

std::string Narrow(const wchar_t *text) {
  if (text == nullptr || text[0] == L'\0') return {};
  int length = WideCharToMultiByte(CP_UTF8, 0, text, -1, nullptr, 0, nullptr, nullptr);
  if (length <= 1) return {};
  std::string result(length - 1, '\0');
  WideCharToMultiByte(CP_UTF8, 0, text, -1, result.data(), length, nullptr, nullptr);
  return result;
}

struct Origin {
  std::string host;
  int port = 0;
};

// http://<loopback>:<port>[/...]: an explicit port, no credentials, printable ASCII.
bool ParseLoopback(const std::string &url, Origin *origin) {
  if (url.size() < 8 || url.size() > 8192) return false;
  std::string lower;
  lower.reserve(url.size());
  for (char c : url) {
    if (c < 0x21 || c > 0x7e) return false;
    lower.push_back((char)std::tolower((unsigned char)c));
  }
  if (lower.compare(0, 7, "http://") != 0) return false;
  size_t end = lower.find_first_of("/?#", 7);
  std::string authority = lower.substr(7, (end == std::string::npos ? lower.size() : end) - 7);
  if (authority.empty() || authority.find('@') != std::string::npos) return false;
  std::string host, port;
  if (authority[0] == '[') {
    size_t close = authority.find(']');
    if (close == std::string::npos || close + 1 >= authority.size() || authority[close + 1] != ':')
      return false;
    host = authority.substr(0, close + 1);
    port = authority.substr(close + 2);
  } else {
    size_t colon = authority.rfind(':');
    if (colon == std::string::npos) return false;
    host = authority.substr(0, colon);
    port = authority.substr(colon + 1);
  }
  if (host != "127.0.0.1" && host != "localhost" && host != "[::1]") return false;
  if (port.empty() || port.size() > 5) return false;
  for (char c : port)
    if (c < '0' || c > '9') return false;
  int value = std::atoi(port.c_str());
  if (value < 1 || value > 65535) return false;
  if (origin != nullptr) {
    origin->host = host;
    origin->port = value;
  }
  return true;
}

bool SameOrigin(const std::string &left, const std::string &right) {
  Origin a, b;
  return ParseLoopback(left, &a) && ParseLoopback(right, &b) && a.host == b.host && a.port == b.port;
}

bool ValidKind(const char *kind) {
  return kind != nullptr && std::strlen(kind) <= 160 &&
         (std::strcmp(kind, "hand") == 0 || std::strcmp(kind, "foot") == 0 ||
          std::strncmp(kind, "hand:", 5) == 0 || std::strncmp(kind, "foot:", 5) == 0);
}

// Minimal reader for the page bridge's own messages: ["name", number] or
// ["name", [number, ...]]. Anything else is ignored.
class MessageReader {
public:
  explicit MessageReader(const std::string &json) : text_(json) {}

  bool Read(std::string *name, std::vector<double> *values, bool *isArray) {
    if (!Expect('[') || !ReadString(name) || !Expect(',')) return false;
    Skip();
    if (Peek() == '[') {
      ++position_;
      *isArray = true;
      Skip();
      if (Peek() == ']') {
        ++position_;
      } else {
        for (;;) {
          double value;
          if (!ReadNumber(&value)) return false;
          values->push_back(value);
          if (values->size() > 8) return false;
          Skip();
          if (Peek() == ',') {
            ++position_;
            continue;
          }
          if (!Expect(']')) return false;
          break;
        }
      }
    } else {
      double value;
      if (!ReadNumber(&value)) return false;
      *isArray = false;
      values->push_back(value);
    }
    if (!Expect(']')) return false;
    Skip();
    return position_ == text_.size();
  }

private:
  char Peek() const { return position_ < text_.size() ? text_[position_] : '\0'; }
  void Skip() {
    while (position_ < text_.size() && std::isspace((unsigned char)text_[position_])) ++position_;
  }
  bool Expect(char c) {
    Skip();
    if (Peek() != c) return false;
    ++position_;
    return true;
  }
  bool ReadString(std::string *out) {
    Skip();
    if (Peek() != '"') return false;
    size_t end = text_.find('"', ++position_);
    if (end == std::string::npos || end - position_ > 32) return false;
    *out = text_.substr(position_, end - position_);
    if (out->find('\\') != std::string::npos) return false;
    position_ = end + 1;
    return true;
  }
  bool ReadNumber(double *out) {
    Skip();
    const char *begin = text_.c_str() + position_;
    char *end = nullptr;
    double value = std::strtod(begin, &end);
    if (end == begin) return false;
    position_ += (size_t)(end - begin);
    *out = value;
    return true;
  }

  const std::string &text_;
  size_t position_ = 0;
};

struct Surface {
  std::string kind;
  unsigned long long id = 0;
  std::string url;
  double viewportWidth = 0, viewportHeight = 0;
  double reportedWidth = 0, reportedHeight = 0;
  double referenceScale = 1;
  double padding = 0;
  RectD contentFrame;
  RectD webFrame;
  RectD selectionInViewport;
  double scale = 0;
  bool customized = false;
  double centerX = 0.5, centerY = 0.5, layoutScale = 1, effectiveScale = 1;
  bool explicitBounds = false;
  RectD normalizedBounds;
  bool selected = false;
  bool hidden = true;
  bool creating = false;
  bool scriptReady = false;
  bool navigated = false;
  bool reloadPending = false;
  // Each viewer has its own retry budget: two pages sharing a renderer must not
  // spend each other's retries when it stops.
  std::deque<std::chrono::steady_clock::time_point> recoveries;

  ComPtr<IDCompositionVisual2> container;
  ComPtr<IDCompositionVisual2> content;
  ComPtr<ICoreWebView2CompositionController> composition;
  ComPtr<ICoreWebView2Controller> controller;
  ComPtr<ICoreWebView2Controller3> controller3;
  ComPtr<ICoreWebView2> webView;
  EventRegistrationToken navigationStarting{}, frameNavigationStarting{}, navigationCompleted{},
      processFailed{}, webMessage{}, newWindow{};
  double appliedScale = 0;
  RECT appliedBounds{};
  bool boundsApplied = false;
  int appliedVisible = -1;
  POINT appliedOffset{INT_MIN, INT_MIN};
  RectD appliedClip;
  std::wstring appliedExclusion;
  int appliedEditing = -1;
};

struct Host {
  HWND window = nullptr;
  DWORD thread = 0;
  int width = 0, height = 0;
  UINT dpi = 96;
  bool iconic = false;
  bool editing = false;
  RectD editorExclusion;
  ComPtr<IDCompositionTarget> target;
  ComPtr<IDCompositionVisual2> root;
  ComPtr<IDCompositionVisual2> webLayer;
  ComPtr<IDCompositionVisual2> chrome;
  ComPtr<IDCompositionSurface> chromeSurface;
  int chromeWidth = 0, chromeHeight = 0;
  // Surface area the last chrome draw covered; redraws touch only old and new outlines.
  RectD chromeDrawn;
  // A new surface's content is undefined until one full draw succeeds.
  bool chromeInitialized = false;
  std::map<std::string, std::unique_ptr<Surface>> surfaces;
};

// Shared across hosts until idle: WebView2 environment, devices and the timer window.
struct Shared {
  DWORD thread = 0;
  ComPtr<ID3D11Device> d3d;
  ComPtr<ID2D1Device> d2d;
  ComPtr<ID2D1Factory> d2dFactory;
  ComPtr<ID2D1StrokeStyle> dashed;
  ComPtr<IDCompositionDesktopDevice> dcomp;
  ComPtr<ICoreWebView2Environment> environment;
  bool environmentCreating = false;
  unsigned long long environmentGeneration = 0;
  HWND messageWindow = nullptr;
  std::deque<std::chrono::steady_clock::time_point> recoveries;
};

Host *host = nullptr;
// Intentionally leaked: static destructors would release WebView2 and D3D objects
// under the loader lock at process exit.
Shared &shared = *new Shared();
bool previewVisible = true;
bool apartmentReady = false;
unsigned long long nextSurfaceId = 1;
HMODULE module = nullptr;

void Layout();
void EnsureControllers();
void CloseWebView(Surface &surface, bool keepVisual = true);
void Commit();

HMODULE Module() {
  if (module == nullptr) {
    // Pin the image: WebView2 callbacks and the timer window procedure may run
    // after a managed host is disposed.
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_PIN,
                       reinterpret_cast<LPCWSTR>(&Module), &module);
  }
  return module;
}

struct WindowSearch {
  DWORD process = 0;
  HWND best = nullptr;
  int bestScore = 0;
  long long bestArea = 0;
};

BOOL CALLBACK FindWindowProc(HWND window, LPARAM parameter) {
  auto *search = reinterpret_cast<WindowSearch *>(parameter);
  DWORD process = 0;
  GetWindowThreadProcessId(window, &process);
  if (process != search->process || !IsWindowVisible(window) || GetWindow(window, GW_OWNER) != nullptr)
    return TRUE;
  wchar_t name[64] = {};
  if (GetClassNameW(window, name, 64) == 0) return TRUE;
  // ADOFAI's Unity player uses UnityWndClass. Never fall back to arbitrary windows.
  int score = std::wcscmp(name, L"UnityWndClass") == 0 ? 100 : std::wcsncmp(name, L"Unity", 5) == 0 ? 50 : 0;
  if (score == 0) return TRUE;
  RECT client{};
  GetClientRect(window, &client);
  if (window == GetForegroundWindow()) score += 5;
  if (client.right >= 640 && client.bottom >= 360) score += 10;
  long long area = (long long)client.right * client.bottom;
  if (score > search->bestScore || (score == search->bestScore && area > search->bestArea)) {
    search->best = window;
    search->bestScore = score;
    search->bestArea = area;
  }
  return TRUE;
}

HWND FindGameWindow(HWND previous) {
  if (previous != nullptr && IsWindow(previous) && IsWindowVisible(previous)) return previous;
  WindowSearch search;
  search.process = GetCurrentProcessId();
  EnumWindows(FindWindowProc, reinterpret_cast<LPARAM>(&search));
  return search.best;
}

bool EnsureApartment() {
  if (apartmentReady) return true;
  HRESULT result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  if (result == RPC_E_CHANGED_MODE) {
    // Fixed for the process lifetime: retrying or returning to the game cannot help.
    SetError("This game build cannot host WebView2 (multithreaded COM apartment).");
    return false;
  }
  if (FAILED(result)) {
    SetError("Windows could not prepare COM for the native viewer.");
    return false;
  }
  // Unity owns this thread for the process lifetime; keep the reference.
  apartmentReady = true;
  return true;
}

void OnTimer(UINT_PTR id);

LRESULT CALLBACK MessageWindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam) {
  if (message == WM_TIMER) {
    KillTimer(window, wParam);
    try {
      OnTimer(wParam);
    } catch (...) {
      SetError("Native viewer operation failed.");
    }
    return 0;
  }
  return DefWindowProcW(window, message, wParam, lParam);
}

HWND EnsureMessageWindow() {
  if (shared.messageWindow != nullptr) return shared.messageWindow;
  WNDCLASSEXW windowClass{sizeof(windowClass)};
  windowClass.lpfnWndProc = MessageWindowProc;
  windowClass.hInstance = Module();
  windowClass.lpszClassName = L"ImplGameWebViewMessages";
  RegisterClassExW(&windowClass);
  shared.messageWindow = CreateWindowExW(0, windowClass.lpszClassName, L"", 0, 0, 0, 0, 0, HWND_MESSAGE,
                                         nullptr, windowClass.hInstance, nullptr);
  return shared.messageWindow;
}

void ReleaseShared() {
  shared.environment.Reset();
  shared.environmentCreating = false;
  ++shared.environmentGeneration;
  shared.dashed.Reset();
  shared.dcomp.Reset();
  shared.d2dFactory.Reset();
  shared.d2d.Reset();
  shared.d3d.Reset();
  shared.recoveries.clear();
}

bool EnsureDevices() {
  if (shared.dcomp) return true;
  const D3D_FEATURE_LEVEL levels[] = {D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0,
                                      D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0};
  HRESULT result = E_FAIL;
  for (D3D_DRIVER_TYPE driver : {D3D_DRIVER_TYPE_HARDWARE, D3D_DRIVER_TYPE_WARP}) {
    result = D3D11CreateDevice(nullptr, driver, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, levels,
                               (UINT)std::size(levels), D3D11_SDK_VERSION, &shared.d3d, nullptr, nullptr);
    if (result == E_INVALIDARG)
      result = D3D11CreateDevice(nullptr, driver, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, levels + 1,
                                 (UINT)std::size(levels) - 1, D3D11_SDK_VERSION, &shared.d3d, nullptr,
                                 nullptr);
    if (SUCCEEDED(result)) break;
  }
  ComPtr<IDXGIDevice> dxgi;
  if (SUCCEEDED(result)) result = shared.d3d.As(&dxgi);
  D2D1_CREATION_PROPERTIES properties{D2D1_THREADING_MODE_SINGLE_THREADED, D2D1_DEBUG_LEVEL_NONE,
                                      D2D1_DEVICE_CONTEXT_OPTIONS_NONE};
  if (SUCCEEDED(result)) result = D2D1CreateDevice(dxgi.Get(), &properties, &shared.d2d);
  if (SUCCEEDED(result)) shared.d2d->GetFactory(&shared.d2dFactory);
  if (SUCCEEDED(result))
    result = DCompositionCreateDevice2(shared.d2d.Get(), IID_PPV_ARGS(&shared.dcomp));
  if (SUCCEEDED(result) && shared.d2dFactory) {
    // Dashes are multiples of the 3-unit stroke: 8 on, 5 off.
    const float dashes[] = {8.0f / 3.0f, 5.0f / 3.0f};
    D2D1_STROKE_STYLE_PROPERTIES stroke = D2D1::StrokeStyleProperties(
        D2D1_CAP_STYLE_FLAT, D2D1_CAP_STYLE_FLAT, D2D1_CAP_STYLE_FLAT, D2D1_LINE_JOIN_ROUND, 10.0f,
        D2D1_DASH_STYLE_CUSTOM, 0.0f);
    shared.d2dFactory->CreateStrokeStyle(stroke, dashes, 2, &shared.dashed);
  }
  if (FAILED(result)) {
    ReleaseShared();
    SetError("Windows could not prepare graphics for the native viewer.");
    return false;
  }
  return true;
}

// Never let WebView2 fall back to its default folder beside the game executable:
// Steam libraries under Program Files are not writable.
std::wstring UserDataFolder() {
  PWSTR local = nullptr;
  std::wstring folder;
  if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_CREATE, nullptr, &local)) && local) {
    folder = std::wstring(local) + L"\\ImplResourcePack\\WebView2";
  }
  CoTaskMemFree(local);
  if (folder.empty()) {
    wchar_t temporary[MAX_PATH + 1] = {};
    DWORD length = GetTempPathW(MAX_PATH + 1, temporary);
    if (length > 0 && length <= MAX_PATH) folder = std::wstring(temporary) + L"ImplResourcePack\\WebView2";
  }
  return folder;
}

const char *RuntimeError(HRESULT result) {
  if (result == HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND))
    return "Microsoft Edge WebView2 Runtime is not installed. Install it and restart ADOFAI.";
  return "WebView2 could not start the in-game viewer. Viewers will reconnect automatically.";
}

// Returns false when recoveries are exhausted: a page that keeps crashing waits
// for the next descriptor or session instead of looping. A viewer's own failure
// spends its budget; environment-wide failures spend the shared one.
bool ScheduleRecovery(std::deque<std::chrono::steady_clock::time_point> &recoveries) {
  auto now = std::chrono::steady_clock::now();
  while (!recoveries.empty() && now - recoveries.front() > kRecoveryWindow) recoveries.pop_front();
  HWND window = EnsureMessageWindow();
  if (window == nullptr || (int)recoveries.size() >= kMaximumRecoveries) return false;
  recoveries.push_back(now);
  SetTimer(window, kRecoverTimer, kRecoverDelayMs, nullptr);
  return true;
}

bool EnsureEnvironment() {
  if (shared.environment || shared.environmentCreating) return true;
  std::wstring folder = UserDataFolder();
  if (folder.empty()) {
    SetError("Windows could not prepare a folder for the in-game viewer. Viewers will reconnect automatically.");
    return false;
  }
  unsigned long long generation = ++shared.environmentGeneration;
  shared.environmentCreating = true;
  HRESULT result = CreateCoreWebView2EnvironmentWithOptions(
      nullptr, folder.c_str(), nullptr,
      Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
          [generation](HRESULT created, ICoreWebView2Environment *environment) -> HRESULT {
            if (generation != shared.environmentGeneration) return S_OK;
            shared.environmentCreating = false;
            if (FAILED(created) || environment == nullptr) {
              SetError(RuntimeError(created));
              if (host != nullptr && created != HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND))
                ScheduleRecovery(shared.recoveries);
              return S_OK;
            }
            // The environment is back; page faults now track each viewer's reload.
            SetError("");
            shared.environment = environment;
            try {
              EnsureControllers();
            } catch (...) {
              SetError("Native viewer operation failed.");
            }
            return S_OK;
          })
          .Get());
  if (FAILED(result)) {
    shared.environmentCreating = false;
    SetError(RuntimeError(result));
    return false;
  }
  return true;
}

Surface *FindSurface(const std::string &kind, unsigned long long id) {
  if (host == nullptr) return nullptr;
  auto found = host->surfaces.find(kind);
  return found != host->surfaces.end() && found->second->id == id ? found->second.get() : nullptr;
}

ICoreWebView2ExecuteScriptCompletedHandler *IgnoreScriptResult() {
  // Leaked like Shared: never released under the loader lock at process exit.
  static ICoreWebView2ExecuteScriptCompletedHandler *handler =
      Callback<ICoreWebView2ExecuteScriptCompletedHandler>([](HRESULT, LPCWSTR) -> HRESULT { return S_OK; })
          .Detach();
  return handler;
}

void Execute(Surface &surface, const std::wstring &script) {
  if (surface.webView && surface.navigated) surface.webView->ExecuteScript(script.c_str(), IgnoreScriptResult());
}

void ApplyEditing(Surface &surface) {
  if (!surface.webView || !surface.navigated || host == nullptr) return;
  int editing = host->editing ? 1 : 0;
  if (surface.appliedEditing == editing) return;
  surface.appliedEditing = editing;
  Execute(surface, editing ? L"window.__irpSetEditing?.(true);" : L"window.__irpSetEditing?.(false);");
}

std::wstring PageScript() {
  std::string script =
      "(() => {\n"
      "if (window.top !== window || window.__irpPost) return;\n"
      "const post = (name, value) => { try { window.chrome.webview.postMessage([name, value]); } catch {} };\n"
      "Object.defineProperty(window, '__irpPost', { value: post });\n"
      // The editor toolbar must stay visible above freely moved viewers. Cut the
      // page itself; the native outline is cleared separately.
      "window.__irpSetExclusion = (x, y, w, h) => {\n"
      "  const root = document.documentElement; if (!root) return;\n"
      "  root.style.clipPath = w > 0 && h > 0\n"
      "    ? `path(evenodd, \"M-100000 -100000H100000V100000H-100000Z M${x} ${y}h${w}v${h}h${-w}Z\")` : '';\n"
      "};\n"
      "const run = () => {\n";
  script += irp::scripts::kSelection;
  script += "\n";
  script += irp::scripts::kPadding;
  script += "\n";
  script += irp::scripts::kLayout;
  script +=
      "\n};\n"
      "if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', run, { once: true });\n"
      "else run();\n"
      "})();";
  return Widen(script);
}

void Navigate(Surface &surface) {
  if (!surface.webView || !surface.scriptReady || surface.url.empty()) return;
  surface.navigated = false;
  surface.appliedEditing = -1;
  surface.appliedExclusion.clear();
  surface.reloadPending = false;
  std::wstring url = Widen(surface.url);
  surface.webView->Navigate(url.c_str());
}

void OnWebMessage(Surface &surface, const std::string &json) {
  std::string name;
  std::vector<double> values;
  bool isArray = false;
  if (!MessageReader(json).Read(&name, &values, &isArray)) return;
  for (double value : values)
    if (!std::isfinite(value)) return;
  if (name == "irpPadding" && !isArray && values.size() == 1) {
    double padding = values[0];
    if (padding < 0 || padding > kMaximumViewport) return;
    if (std::abs(surface.padding - padding) > 0.001) {
      surface.padding = padding;
      ++geometryRevision;
      Layout();
    }
  } else if (name == "irpLayout" && isArray && values.size() == 3) {
    for (double value : values)
      if (value < 0 || value > kMaximumViewport) return;
    if (values[0] < 1 || values[1] < 1 || values[2] * 2 >= std::min(values[0], values[1])) return;
    surface.reportedWidth = values[0];
    surface.reportedHeight = values[1];
    if (surface.viewportWidth != values[0] || surface.viewportHeight != values[1] ||
        std::abs(surface.padding - values[2]) > 0.001) {
      surface.viewportWidth = values[0];
      surface.viewportHeight = values[1];
      surface.padding = values[2];
      surface.selectionInViewport = {};
      ++geometryRevision;
      Layout();
    }
  } else if (name == "irpSelection" && isArray && values.size() == 4) {
    for (double value : values)
      if (value < 0 || value > 1) return;
    if (values[0] + values[2] > 1.001 || values[1] + values[3] > 1.001) return;
    surface.selectionInViewport = {values[0], values[1], values[2], values[3]};
  }
}

// Takes ownership of the COM-allocated URI.
bool AllowedNavigation(const Surface &surface, LPWSTR uri) {
  std::string target = Narrow(uri);
  CoTaskMemFree(uri);
  return SameOrigin(surface.url, target);
}

// Pages and their frames may navigate only within the descriptor's origin. The
// block stays on this viewer until its page loads again.
ComPtr<ICoreWebView2NavigationStartingEventHandler> SameOriginGuard(const std::string &kind,
                                                                   unsigned long long id) {
  return Callback<ICoreWebView2NavigationStartingEventHandler>(
      [kind, id](ICoreWebView2 *, ICoreWebView2NavigationStartingEventArgs *args) -> HRESULT {
        Surface *current = FindSurface(kind, id);
        LPWSTR uri = nullptr;
        args->get_Uri(&uri);
        if (current == nullptr || !AllowedNavigation(*current, uri)) {
          args->put_Cancel(TRUE);
          if (current != nullptr) SetFault(kind, "Native viewer blocked navigation outside its local source.");
        }
        return S_OK;
      });
}

void ConfigureWebView(Surface &surface) {
  std::string kind = surface.kind;
  unsigned long long id = surface.id;
  ComPtr<ICoreWebView2Settings> settings;
  if (SUCCEEDED(surface.webView->get_Settings(&settings))) {
    settings->put_AreDefaultContextMenusEnabled(FALSE);
    settings->put_AreDefaultScriptDialogsEnabled(FALSE);
    settings->put_AreDevToolsEnabled(FALSE);
    settings->put_IsStatusBarEnabled(FALSE);
    settings->put_IsZoomControlEnabled(FALSE);
    settings->put_IsBuiltInErrorPageEnabled(FALSE);
    ComPtr<ICoreWebView2Settings3> settings3;
    if (SUCCEEDED(settings.As(&settings3))) settings3->put_AreBrowserAcceleratorKeysEnabled(FALSE);
    ComPtr<ICoreWebView2Settings4> settings4;
    if (SUCCEEDED(settings.As(&settings4))) {
      settings4->put_IsGeneralAutofillEnabled(FALSE);
      settings4->put_IsPasswordAutosaveEnabled(FALSE);
    }
    ComPtr<ICoreWebView2Settings5> settings5;
    if (SUCCEEDED(settings.As(&settings5))) settings5->put_IsPinchZoomEnabled(FALSE);
    ComPtr<ICoreWebView2Settings6> settings6;
    if (SUCCEEDED(settings.As(&settings6))) settings6->put_IsSwipeNavigationEnabled(FALSE);
  }

  surface.webView->add_NavigationStarting(SameOriginGuard(kind, id).Get(), &surface.navigationStarting);
  surface.webView->add_FrameNavigationStarting(SameOriginGuard(kind, id).Get(), &surface.frameNavigationStarting);
  surface.webView->add_NewWindowRequested(
      Callback<ICoreWebView2NewWindowRequestedEventHandler>(
          [](ICoreWebView2 *, ICoreWebView2NewWindowRequestedEventArgs *args) -> HRESULT {
            args->put_Handled(TRUE);
            return S_OK;
          })
          .Get(),
      &surface.newWindow);
  surface.webView->add_NavigationCompleted(
      Callback<ICoreWebView2NavigationCompletedEventHandler>(
          [kind, id](ICoreWebView2 *, ICoreWebView2NavigationCompletedEventArgs *args) -> HRESULT {
            Surface *current = FindSurface(kind, id);
            if (current == nullptr) return S_OK;
            BOOL success = FALSE;
            args->get_IsSuccess(&success);
            if (!success) {
              COREWEBVIEW2_WEB_ERROR_STATUS status = COREWEBVIEW2_WEB_ERROR_STATUS_UNKNOWN;
              args->get_WebErrorStatus(&status);
              if (status != COREWEBVIEW2_WEB_ERROR_STATUS_OPERATION_CANCELED) {
                // ImplDmNote's page server may be restarting: retry like a stopped
                // renderer, then wait for the next descriptor.
                if (ScheduleRecovery(current->recoveries)) {
                  current->reloadPending = true;
                  SetFault(kind, kLoadRetrying);
                } else {
                  current->url.clear();
                  SetFault(kind, kLoadFailed);
                }
              }
              return S_OK;
            }
            current->navigated = true;
            SetFault(kind, "");
            ApplyEditing(*current);
            Layout();
            return S_OK;
          })
          .Get(),
      &surface.navigationCompleted);
  surface.webView->add_ProcessFailed(
      Callback<ICoreWebView2ProcessFailedEventHandler>(
          [kind, id](ICoreWebView2 *, ICoreWebView2ProcessFailedEventArgs *args) -> HRESULT {
            COREWEBVIEW2_PROCESS_FAILED_KIND failure = COREWEBVIEW2_PROCESS_FAILED_KIND_UNKNOWN_PROCESS_EXITED;
            args->get_ProcessFailedKind(&failure);
            if (failure == COREWEBVIEW2_PROCESS_FAILED_KIND_BROWSER_PROCESS_EXITED) {
              // Every controller of this environment is gone. Tear down outside this
              // event, keep descriptors, and rebuild after a short delay. The
              // teardown marks every viewer until its page loads again.
              if (HWND window = EnsureMessageWindow()) SetTimer(window, kTeardownTimer, 0, nullptr);
            } else if (failure == COREWEBVIEW2_PROCESS_FAILED_KIND_RENDER_PROCESS_EXITED) {
              if (Surface *current = FindSurface(kind, id)) {
                current->navigated = false;
                SetFault(kind, kRendererStopped);
                // Without a retry, the next descriptor for this URL reloads it.
                if (ScheduleRecovery(current->recoveries)) current->reloadPending = true;
                else current->url.clear();
              }
            }
            return S_OK;
          })
          .Get(),
      &surface.processFailed);
  surface.webView->add_WebMessageReceived(
      Callback<ICoreWebView2WebMessageReceivedEventHandler>(
          [kind, id](ICoreWebView2 *, ICoreWebView2WebMessageReceivedEventArgs *args) -> HRESULT {
            Surface *current = FindSurface(kind, id);
            if (current == nullptr) return S_OK;
            LPWSTR source = nullptr;
            args->get_Source(&source);
            if (!AllowedNavigation(*current, source)) return S_OK;
            LPWSTR json = nullptr;
            if (SUCCEEDED(args->get_WebMessageAsJson(&json)) && json != nullptr) {
              std::string message = Narrow(json);
              CoTaskMemFree(json);
              if (message.size() <= 512) OnWebMessage(*current, message);
            }
            return S_OK;
          })
          .Get(),
      &surface.webMessage);

  std::wstring script = PageScript();
  surface.webView->AddScriptToExecuteOnDocumentCreated(
      script.c_str(),
      Callback<ICoreWebView2AddScriptToExecuteOnDocumentCreatedCompletedHandler>(
          [kind, id](HRESULT result, LPCWSTR) -> HRESULT {
            Surface *current = FindSurface(kind, id);
            if (current == nullptr) return S_OK;
            if (FAILED(result)) {
              SetFault(kind, "Native viewer could not prepare its page. Try again.");
              return S_OK;
            }
            current->scriptReady = true;
            Navigate(*current);
            return S_OK;
          })
          .Get());
}

void OnControllerCreated(Surface &surface, ICoreWebView2CompositionController *composition) {
  surface.composition = composition;
  if (FAILED(surface.composition.As(&surface.controller)) || FAILED(surface.controller.As(&surface.controller3)) ||
      FAILED(surface.controller->get_CoreWebView2(&surface.webView))) {
    CloseWebView(surface);
    SetFault(surface.kind, kRuntimeTooOld);
    return;
  }
  ComPtr<ICoreWebView2Controller2> controller2;
  if (SUCCEEDED(surface.controller.As(&controller2))) controller2->put_DefaultBackgroundColor({0, 0, 0, 0});
  ComPtr<ICoreWebView2Controller4> controller4;
  if (SUCCEEDED(surface.controller.As(&controller4))) controller4->put_AllowExternalDrop(FALSE);
  // The host owns the CSS-to-pixel scale; monitor DPI must not override it.
  surface.controller3->put_ShouldDetectMonitorScaleChanges(FALSE);
  surface.controller3->put_BoundsMode(COREWEBVIEW2_BOUNDS_MODE_USE_RAW_PIXELS);
  surface.controller->put_IsVisible(FALSE);
  surface.appliedVisible = 0;
  surface.composition->put_RootVisualTarget(surface.content.Get());
  ConfigureWebView(surface);
  Layout();
}

void CreateController(Surface &surface) {
  if (surface.controller || surface.creating || !shared.environment || host == nullptr) return;
  std::string kind = surface.kind;
  unsigned long long id = surface.id;
  auto completed = Callback<ICoreWebView2CreateCoreWebView2CompositionControllerCompletedHandler>(
      [kind, id](HRESULT result, ICoreWebView2CompositionController *composition) -> HRESULT {
        Surface *current = FindSurface(kind, id);
        if (current == nullptr) {
          ComPtr<ICoreWebView2Controller> orphan;
          if (composition != nullptr && SUCCEEDED(composition->QueryInterface(IID_PPV_ARGS(&orphan))))
            orphan->Close();
          return S_OK;
        }
        current->creating = false;
        if (FAILED(result) || composition == nullptr) {
          SetFault(kind, "Native viewer could not create its page. Viewers will reconnect automatically.");
          ScheduleRecovery(current->recoveries);
          return S_OK;
        }
        try {
          OnControllerCreated(*current, composition);
        } catch (...) {
          SetError("Native viewer operation failed.");
        }
        return S_OK;
      });
  HRESULT result = E_NOINTERFACE;
  ComPtr<ICoreWebView2Environment10> environment10;
  if (SUCCEEDED(shared.environment.As(&environment10))) {
    // Like WebKit's non-persistent store: no cookies or storage outlive the page.
    ComPtr<ICoreWebView2ControllerOptions> options;
    if (SUCCEEDED(environment10->CreateCoreWebView2ControllerOptions(&options))) {
      options->put_IsInPrivateModeEnabled(TRUE);
      ComPtr<ICoreWebView2ControllerOptions3> options3;
      if (SUCCEEDED(options.As(&options3))) options3->put_DefaultBackgroundColor({0, 0, 0, 0});
      result = environment10->CreateCoreWebView2CompositionControllerWithOptions(host->window, options.Get(),
                                                                                  completed.Get());
    }
  }
  if (FAILED(result)) {
    ComPtr<ICoreWebView2Environment3> environment3;
    if (SUCCEEDED(shared.environment.As(&environment3)))
      result = environment3->CreateCoreWebView2CompositionController(host->window, completed.Get());
  }
  if (FAILED(result)) {
    SetFault(surface.kind, kRuntimeTooOld);
    return;
  }
  surface.creating = true;
}

void EnsureControllers() {
  if (host == nullptr || !shared.environment) return;
  for (auto &entry : host->surfaces) CreateController(*entry.second);
}

void CloseWebView(Surface &surface, bool keepVisual) {
  if (surface.webView) {
    surface.webView->remove_NavigationStarting(surface.navigationStarting);
    surface.webView->remove_FrameNavigationStarting(surface.frameNavigationStarting);
    surface.webView->remove_NavigationCompleted(surface.navigationCompleted);
    surface.webView->remove_ProcessFailed(surface.processFailed);
    surface.webView->remove_WebMessageReceived(surface.webMessage);
    surface.webView->remove_NewWindowRequested(surface.newWindow);
    surface.webView->Stop();
  }
  if (surface.controller) surface.controller->Close();
  surface.webView.Reset();
  surface.controller3.Reset();
  surface.controller.Reset();
  surface.composition.Reset();
  surface.creating = false;
  surface.scriptReady = false;
  surface.navigated = false;
  surface.reloadPending = false;
  surface.appliedScale = 0;
  surface.boundsApplied = false;
  surface.appliedVisible = -1;
  surface.appliedExclusion.clear();
  surface.appliedEditing = -1;
  // A new controller replaces the visual's children; give it an empty content visual.
  if (surface.container && surface.content) surface.container->RemoveVisual(surface.content.Get());
  surface.content.Reset();
  if (keepVisual && surface.container && shared.dcomp && SUCCEEDED(shared.dcomp->CreateVisual(&surface.content)))
    surface.container->AddVisual(surface.content.Get(), FALSE, nullptr);
}

void RemoveSurface(const std::string &kind) {
  if (host == nullptr) return;
  auto found = host->surfaces.find(kind);
  if (found == host->surfaces.end()) return;
  Surface &surface = *found->second;
  CloseWebView(surface, false);
  if (surface.container) host->webLayer->RemoveVisual(surface.container.Get());
  host->surfaces.erase(found);
  SetFault(kind, "");
}

bool AttachTarget(HWND window) {
  host->target.Reset();
  if (FAILED(shared.dcomp->CreateTargetForHwnd(window, TRUE, &host->target)) ||
      FAILED(host->target->SetRoot(host->root.Get()))) {
    host->target.Reset();
    SetError("The game window does not accept the in-game viewer layer. Return to the game and retry.");
    return false;
  }
  host->window = window;
  for (auto &entry : host->surfaces)
    if (entry.second->controller) entry.second->controller->put_ParentWindow(window);
  return true;
}

// Returns true when the game viewport changed size or density.
bool RefreshMetrics(bool *visibilityChanged) {
  RECT client{};
  GetClientRect(host->window, &client);
  UINT dpi = GetDpiForWindow(host->window);
  bool iconic = IsIconic(host->window) != FALSE;
  bool changed = client.right != host->width || client.bottom != host->height || (dpi != 0 && dpi != host->dpi);
  *visibilityChanged = iconic != host->iconic;
  host->width = client.right;
  host->height = client.bottom;
  if (dpi != 0) host->dpi = dpi;
  host->iconic = iconic;
  return changed;
}

void DestroyHost() {
  if (host == nullptr) return;
  std::vector<std::string> kinds;
  for (auto &entry : host->surfaces) kinds.push_back(entry.first);
  for (const std::string &kind : kinds) RemoveSurface(kind);
  if (host->chrome) host->chrome->SetContent(nullptr);
  host->chromeSurface.Reset();
  if (host->target) host->target->SetRoot(nullptr);
  Commit();
  delete host;
  host = nullptr;
}

bool EnsureHost() {
  HWND window = FindGameWindow(host != nullptr ? host->window : nullptr);
  if (window == nullptr) {
    SetError("The Unity game window was not found. Return to the game and retry.");
    return false;
  }
  DWORD thread = GetWindowThreadProcessId(window, nullptr);
  if (thread != GetCurrentThreadId()) {
    SetError("Native viewers must be created on the game window's thread.");
    return false;
  }
  if (!EnsureApartment() || !EnsureDevices()) return false;
  if (shared.thread != 0 && shared.thread != thread) {
    SetError("Native viewers must be created on the game window's thread.");
    return false;
  }
  shared.thread = thread;
  if (EnsureMessageWindow() != nullptr) KillTimer(shared.messageWindow, kIdleTimer);
  if (host == nullptr) {
    host = new Host();
    host->thread = thread;
    if (FAILED(shared.dcomp->CreateVisual(&host->root)) || FAILED(shared.dcomp->CreateVisual(&host->webLayer)) ||
        FAILED(shared.dcomp->CreateVisual(&host->chrome)) ||
        FAILED(host->root->AddVisual(host->webLayer.Get(), FALSE, nullptr)) ||
        FAILED(host->root->AddVisual(host->chrome.Get(), TRUE, host->webLayer.Get()))) {
      delete host;
      host = nullptr;
      SetError("Windows could not prepare graphics for the native viewer.");
      return false;
    }
  }
  if (host->window != window && !AttachTarget(window)) return false;
  bool visibilityChanged = false;
  RefreshMetrics(&visibilityChanged);
  Layout();
  return true;
}

void Commit() {
  if (shared.dcomp) shared.dcomp->Commit();
}

void ApplyVisibility(Surface &surface) {
  if (!surface.controller) return;
  int visible = surface.hidden ? 0 : 1;
  if (surface.appliedVisible == visible) return;
  surface.appliedVisible = visible;
  // A hidden WebView2 stops rendering and throttles timers like a background tab,
  // while keeping the page loaded for an immediate return.
  surface.controller->put_IsVisible(visible ? TRUE : FALSE);
}

void ApplySurface(Surface &surface, const RectD &clip) {
  if (!surface.container) return;
  POINT offset{(LONG)std::lround(surface.webFrame.x), (LONG)std::lround(surface.webFrame.y)};
  if (offset.x != surface.appliedOffset.x || offset.y != surface.appliedOffset.y) {
    // Whole-pixel offsets keep text sharp: the page already rasterizes at its final scale.
    surface.container->SetOffsetX((float)offset.x);
    surface.container->SetOffsetY((float)offset.y);
    surface.appliedOffset = offset;
  }
  if (!SameRect(clip, surface.appliedClip)) {
    D2D_RECT_F rect{(float)std::round(clip.x), (float)std::round(clip.y), (float)std::round(clip.Right()),
                    (float)std::round(clip.Bottom())};
    surface.container->SetClip(rect);
    surface.appliedClip = clip;
  }
  if (!surface.controller) return;
  if (std::abs(surface.appliedScale - surface.scale) > 0.000001) {
    surface.controller3->put_RasterizationScale(surface.scale);
    surface.appliedScale = surface.scale;
  }
  RECT bounds{offset.x, offset.y, offset.x + std::max<LONG>(1, std::lround(surface.webFrame.width)),
              offset.y + std::max<LONG>(1, std::lround(surface.webFrame.height))};
  if (!surface.boundsApplied || !EqualRect(&bounds, &surface.appliedBounds)) {
    surface.controller->put_Bounds(bounds);
    surface.appliedBounds = bounds;
    surface.boundsApplied = true;
  }
  ApplyVisibility(surface);
}

RectD ExclusionPixels() {
  if (host == nullptr || !host->editing || host->editorExclusion.Empty()) return {};
  const RectD &normalized = host->editorExclusion;
  return Intersect({0, 0, (double)host->width, (double)host->height},
                   {normalized.x * host->width, normalized.y * host->height, normalized.width * host->width,
                    normalized.height * host->height});
}

void ApplyExclusion(Surface &surface, const RectD &exclusion) {
  if (!surface.webView || !surface.navigated) return;
  RectD cut = surface.hidden || surface.scale <= 0 ? RectD{} : Intersect(exclusion, surface.webFrame);
  wchar_t script[256];
  if (cut.Empty()) {
    std::swprintf(script, 256, L"window.__irpSetExclusion?.(0,0,0,0);");
  } else {
    double offsetX = (double)surface.appliedOffset.x, offsetY = (double)surface.appliedOffset.y;
    std::swprintf(script, 256, L"window.__irpSetExclusion?.(%.2f,%.2f,%.2f,%.2f);",
                  (cut.x - offsetX) / surface.scale, (cut.y - offsetY) / surface.scale,
                  cut.width / surface.scale, cut.height / surface.scale);
  }
  if (surface.appliedExclusion == script) return;
  surface.appliedExclusion = script;
  Execute(surface, script);
}

void DrawChrome(ID2D1DeviceContext *context) {
  double unit = std::clamp(host->height / 1000.0, 1.0, 2.0);
  ComPtr<ID2D1SolidColorBrush> white, shadow;
  context->CreateSolidColorBrush(D2D1::ColorF(1, 1, 1, 1), &white);
  context->CreateSolidColorBrush(D2D1::ColorF(0, 0, 0, 0.75f), &shadow);
  if (!white || !shadow) return;
  for (auto &entry : host->surfaces) {
    Surface &surface = *entry.second;
    if (surface.hidden || !surface.selected) continue;
    const RectD &frame = surface.contentFrame;
    RectD inset = Inflate(frame, -1.5 * unit);
    double radius = std::min(8 * unit, std::min(inset.width, inset.height) / 2);
    if (radius > 0) {
      D2D1_ROUNDED_RECT outline{D2D1::RectF((float)inset.x, (float)inset.y, (float)inset.Right(), (float)inset.Bottom()),
                                (float)radius, (float)radius};
      context->DrawRoundedRectangle(outline, white.Get(), (float)(3 * unit), shared.dashed.Get());
    }
    double midX = frame.x + frame.width / 2, midY = frame.y + frame.height / 2;
    const double points[8][2] = {{frame.x, frame.y},        {midX, frame.y},           {frame.Right(), frame.y},
                                 {frame.Right(), midY},     {frame.Right(), frame.Bottom()}, {midX, frame.Bottom()},
                                 {frame.x, frame.Bottom()}, {frame.x, midY}};
    for (const auto &point : points) {
      D2D1_ELLIPSE handle{D2D1::Point2F((float)point[0], (float)point[1]), (float)(3.5 * unit), (float)(3.5 * unit)};
      context->FillEllipse(handle, white.Get());
      context->DrawEllipse(handle, shadow.Get(), (float)unit);
    }
  }
}

RectD Union(const RectD &left, const RectD &right) {
  if (left.Empty()) return right;
  if (right.Empty()) return left;
  double l = std::min(left.x, right.x), t = std::min(left.y, right.y);
  return {l, t, std::max(left.Right(), right.Right()) - l, std::max(left.Bottom(), right.Bottom()) - t};
}

// Everything DrawChrome can touch: outlines plus handle radius, stroke and antialiasing.
RectD ChromeExtent() {
  double unit = std::clamp(host->height / 1000.0, 1.0, 2.0);
  RectD extent;
  for (auto &entry : host->surfaces) {
    const Surface &surface = *entry.second;
    if (!surface.hidden && surface.selected) extent = Union(extent, Inflate(surface.contentFrame, 5 * unit + 2));
  }
  return Intersect(extent, {0, 0, (double)host->width, (double)host->height});
}

void UpdateChrome() {
  bool show = host->editing && previewVisible && !host->iconic && host->width > 0 && host->height > 0;
  if (!show) {
    if (host->chromeSurface) {
      host->chrome->SetContent(nullptr);
      host->chromeSurface.Reset();
    }
    host->chromeDrawn = {};
    host->chromeInitialized = false;
    return;
  }
  if (!host->chromeSurface || host->chromeWidth != host->width || host->chromeHeight != host->height) {
    host->chromeSurface.Reset();
    if (FAILED(shared.dcomp->CreateSurface(host->width, host->height, DXGI_FORMAT_B8G8R8A8_UNORM,
                                           DXGI_ALPHA_MODE_PREMULTIPLIED, &host->chromeSurface)))
      return;
    host->chromeWidth = host->width;
    host->chromeHeight = host->height;
    host->chrome->SetContent(host->chromeSurface.Get());
    host->chromeInitialized = false;
  }
  // Dragging redraws every frame; at 4K a full-window draw is ~33 MB of fill.
  // Only the previous and the new outlines change, so update just their union.
  RectD extent = ChromeExtent();
  RectD dirty = host->chromeInitialized ? Union(host->chromeDrawn, extent)
                                        : RectD{0, 0, (double)host->width, (double)host->height};
  if (dirty.Empty()) return;
  RECT update{(LONG)std::floor(dirty.x), (LONG)std::floor(dirty.y), (LONG)std::ceil(dirty.Right()),
              (LONG)std::ceil(dirty.Bottom())};
  update.left = std::max<LONG>(0, update.left);
  update.top = std::max<LONG>(0, update.top);
  update.right = std::min<LONG>(host->width, update.right);
  update.bottom = std::min<LONG>(host->height, update.bottom);
  if (update.right <= update.left || update.bottom <= update.top) return;
  ComPtr<ID2D1DeviceContext> context;
  POINT offset{};
  if (FAILED(host->chromeSurface->BeginDraw(&update, IID_PPV_ARGS(&context), &offset))) return;
  // The offset locates the update rectangle, not the surface origin.
  context->SetTransform(
      D2D1::Matrix3x2F::Translation((float)(offset.x - update.left), (float)(offset.y - update.top)));
  context->PushAxisAlignedClip(
      D2D1::RectF((float)update.left, (float)update.top, (float)update.right, (float)update.bottom),
      D2D1_ANTIALIAS_MODE_ALIASED);
  context->Clear(D2D1::ColorF(0, 0, 0, 0));
  DrawChrome(context.Get());
  RectD exclusion = ExclusionPixels();
  if (!exclusion.Empty()) {
    context->PushAxisAlignedClip(D2D1::RectF((float)exclusion.x, (float)exclusion.y, (float)exclusion.Right(),
                                             (float)exclusion.Bottom()),
                                 D2D1_ANTIALIAS_MODE_ALIASED);
    context->Clear(D2D1::ColorF(0, 0, 0, 0));
    context->PopAxisAlignedClip();
  }
  context->PopAxisAlignedClip();
  if (FAILED(host->chromeSurface->EndDraw())) return;
  host->chromeDrawn = extent;
  host->chromeInitialized = true;
}

void Layout() {
  if (host == nullptr) return;
  double width = host->width, height = host->height;
  double unit = host->dpi / 96.0;
  for (auto &entry : host->surfaces) {
    const std::string &kind = entry.first;
    Surface &surface = *entry.second;
    double sourceWidth = surface.viewportWidth, sourceHeight = surface.viewportHeight;
    if (sourceWidth <= 0 || sourceHeight <= 0) continue;
    double padding = std::min(surface.padding, (std::min(sourceWidth, sourceHeight) - 1) / 2);
    double contentWidth = sourceWidth - padding * 2, contentHeight = sourceHeight - padding * 2;
    // Fit once on attachment, then preserve CSS pixel size as content grows.
    // Automatic placement measures in DIPs, like AppKit points.
    double baseScale = surface.referenceScale * unit;
    double scale = surface.customized ? baseScale * surface.layoutScale : baseScale;
    if (!surface.explicitBounds) surface.effectiveScale = baseScale > 0 ? scale / baseScale : 1;
    if (surface.explicitBounds) scale = surface.normalizedBounds.width * width / contentWidth;
    bool canDisplay = previewVisible && !host->iconic && width > 0 && height > 0 && scale > 0 && std::isfinite(scale);
    surface.hidden = !canDisplay;
    if (!canDisplay) {
      ApplyVisibility(surface);
      continue;
    }
    double w = contentWidth * scale, h = contentHeight * scale;
    bool hand = kind == "hand" || kind.compare(0, 5, "hand:") == 0;
    double x = hand ? kInset * unit : width - kInset * unit - w;
    double y = height - kInset * unit - h;
    if (surface.customized) {
      x = std::clamp(surface.centerX * width - w / 2, -w * (1 - kMinimumVisibleFraction),
                     width - w * kMinimumVisibleFraction);
      y = std::clamp(surface.centerY * height - h / 2, -h * (1 - kMinimumVisibleFraction),
                     height - h * kMinimumVisibleFraction);
    }
    RectD frame{x, y, w, h};
    if (surface.explicitBounds) {
      const RectD &normalized = surface.normalizedBounds;
      frame = {normalized.x * width, normalized.y * height, normalized.width * width, normalized.height * height};
    }
    surface.contentFrame = frame;
    surface.scale = scale;
    double inset = padding * scale;
    surface.webFrame = Inflate(frame, inset);
    ApplySurface(surface, {inset, inset, frame.width, frame.height});
  }
  UpdateChrome();
  RectD exclusion = ExclusionPixels();
  for (auto &entry : host->surfaces) ApplyExclusion(*entry.second, exclusion);
  Commit();
}

void Maintain() {
  if (host == nullptr || host->thread != GetCurrentThreadId()) return;
  if (!IsWindow(host->window)) {
    // Unity can recreate its window (display or mode changes); follow it.
    HWND window = FindGameWindow(nullptr);
    if (window == nullptr || GetWindowThreadProcessId(window, nullptr) != host->thread || !AttachTarget(window))
      return;
    ++geometryRevision;
  }
  bool visibilityChanged = false;
  if (RefreshMetrics(&visibilityChanged)) ++geometryRevision;
  else if (!visibilityChanged) return;
  Layout();
}

void OnTimer(UINT_PTR id) {
  if (id == kIdleTimer) {
    if (host == nullptr) ReleaseShared();
    return;
  }
  if (id == kTeardownTimer) {
    if (host != nullptr)
      for (auto &entry : host->surfaces) {
        CloseWebView(*entry.second);
        SetFault(entry.first, kRendererStopped);
      }
    shared.environment.Reset();
    shared.environmentCreating = false;
    ++shared.environmentGeneration;
    Commit();
    // Without a retry, the next descriptor recreates the environment.
    if (host != nullptr) ScheduleRecovery(shared.recoveries);
    return;
  }
  if (id != kRecoverTimer || host == nullptr) return;
  if (!shared.environment) {
    EnsureEnvironment();
    return;
  }
  EnsureControllers();
  for (auto &entry : host->surfaces) {
    Surface &surface = *entry.second;
    if (surface.reloadPending && surface.webView) Navigate(surface);
  }
}

template <class Work> int Guarded(Work &&work) {
  try {
    return work();
  } catch (...) {
    SetError("Native viewer operation failed.");
    return 0;
  }
}

bool OnGameThread() {
  if (host != nullptr && host->thread != GetCurrentThreadId()) {
    SetError("Native viewers must be called on the game window's thread.");
    return false;
  }
  return true;
}

Surface *Lookup(const char *kind) {
  if (host == nullptr || kind == nullptr) return nullptr;
  auto found = host->surfaces.find(kind);
  return found == host->surfaces.end() ? nullptr : found->second.get();
}
} // namespace

extern "C" {

IRP_WEBVIEW_API int irp_webview_supported(void) {
  return Guarded([] {
    LPWSTR version = nullptr;
    HRESULT result = GetAvailableCoreWebView2BrowserVersionString(nullptr, &version);
    bool available = SUCCEEDED(result) && version != nullptr && version[0] != L'\0';
    CoTaskMemFree(version);
    if (!available) {
      SetError("Microsoft Edge WebView2 Runtime is not installed. Install it and restart ADOFAI.");
      return 0;
    }
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_set_surface(const char *viewerKind, const char *url, int width, int height) {
  if (!irp_webview_supported()) return 0;
  if (!ValidKind(viewerKind)) {
    SetError("Native viewer identifier must begin with hand or foot.");
    return 0;
  }
  bool remove = url == nullptr || url[0] == '\0';
  if (!remove && (width < 1 || height < 1 || width > kMaximumViewport || height > kMaximumViewport)) {
    SetError("Native viewer requires a local HTTP URL and a viewport from 1 to 4096.");
    return 0;
  }
  if (!OnGameThread()) return 0;
  return Guarded([&] {
    std::string kind = viewerKind;
    if (remove) {
      RemoveSurface(kind);
      Commit();
      SetError("");
      return 1;
    }
    std::string source = url;
    if (!ParseLoopback(source, nullptr)) {
      SetError("Native viewer requires a local HTTP URL and a viewport from 1 to 4096.");
      return 0;
    }
    if (!EnsureHost() || !EnsureEnvironment()) return 0;
    std::unique_ptr<Surface> &slot = host->surfaces[kind];
    if (!slot) {
      slot = std::make_unique<Surface>();
      Surface &created = *slot;
      created.kind = kind;
      created.id = nextSurfaceId++;
      created.referenceScale = std::min(1.0, std::min(kMaximumWidth / width, kMaximumHeight / height));
      if (FAILED(shared.dcomp->CreateVisual(&created.container)) ||
          FAILED(shared.dcomp->CreateVisual(&created.content)) ||
          FAILED(created.container->AddVisual(created.content.Get(), FALSE, nullptr)) ||
          FAILED(host->webLayer->AddVisual(created.container.Get(), TRUE, nullptr))) {
        host->surfaces.erase(kind);
        SetError("Windows could not prepare graphics for the native viewer.");
        return 0;
      }
    }
    Surface &surface = *slot;
    bool sourceChanged = surface.url != source;
    if (sourceChanged) surface.reportedWidth = surface.reportedHeight = 0;
    // Descriptors bootstrap the page. Once it reports its computed layout,
    // repeated descriptors must not restore obsolete desktop window bounds.
    double viewportWidth = surface.reportedWidth > 0 ? surface.reportedWidth : width;
    double viewportHeight = surface.reportedWidth > 0 ? surface.reportedHeight : height;
    if (surface.viewportWidth != viewportWidth || surface.viewportHeight != viewportHeight) ++geometryRevision;
    surface.viewportWidth = viewportWidth;
    surface.viewportHeight = viewportHeight;
    if (sourceChanged) {
      if (surface.padding != 0) ++geometryRevision;
      surface.padding = 0;
      surface.selectionInViewport = {};
      surface.url = source;
      // The retry budget is kept: a page that keeps failing gets about one retry
      // per descriptor poll instead of a fresh burst each time.
      SetFault(kind, "");
      if (surface.webView) Navigate(surface);
    } else if (surface.reloadPending && surface.webView) {
      Navigate(surface);
    }
    // No-op while a controller is being created or the environment is starting;
    // also rebuilds a page after its browser process stopped.
    if (!surface.webView) CreateController(surface);
    Layout();
    // The call succeeded; earlier call errors no longer describe the host.
    SetError("");
    return 1;
  });
}

IRP_WEBVIEW_API void irp_webview_set_visible(int visible) {
  if (!OnGameThread()) return;
  Guarded([&] {
    previewVisible = visible != 0;
    Layout();
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_set_layout(const char *viewerKind, int customized, double centerX, double centerY,
                                           double scale) {
  if (viewerKind == nullptr || !std::isfinite(centerX) || !std::isfinite(centerY) || !std::isfinite(scale) ||
      scale <= 0) {
    SetError("Native viewer layout requires a valid viewer and finite positive size.");
    return 0;
  }
  if (!OnGameThread()) return 0;
  return Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface == nullptr) return 0;
    surface->explicitBounds = false;
    surface->customized = customized != 0;
    surface->centerX = std::clamp(centerX, -4.0, 5.0);
    surface->centerY = std::clamp(centerY, -4.0, 5.0);
    surface->layoutScale = std::clamp(scale, 0.2, 5.0);
    Layout();
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_set_bounds(const char *viewerKind, double x, double y, double width, double height,
                                           double effectiveScale) {
  if (viewerKind == nullptr || !std::isfinite(x) || !std::isfinite(y) || !std::isfinite(width) ||
      !std::isfinite(height) || !std::isfinite(effectiveScale) || width <= 0 || height <= 0 || effectiveScale <= 0) {
    SetError("Native viewer bounds require finite coordinates and positive size.");
    return 0;
  }
  if (!OnGameThread()) return 0;
  return Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface == nullptr) return 0;
    RectD normalized{x, y, width, height};
    bool changed = !surface->explicitBounds || !SameRect(surface->normalizedBounds, normalized) ||
                   surface->effectiveScale != effectiveScale;
    surface->explicitBounds = true;
    surface->normalizedBounds = normalized;
    surface->effectiveScale = effectiveScale;
    if (changed) Layout();
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_get_reference_size(const char *viewerKind, double *width, double *height) {
  if (viewerKind == nullptr || width == nullptr || height == nullptr || !OnGameThread()) return 0;
  return Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface == nullptr || surface->viewportWidth <= 0 || surface->viewportHeight <= 0) return 0;
    double padding =
        std::min(surface->padding, (std::min(surface->viewportWidth, surface->viewportHeight) - 1) / 2);
    *width = (surface->viewportWidth - 2 * padding) * surface->referenceScale;
    *height = (surface->viewportHeight - 2 * padding) * surface->referenceScale;
    return 1;
  });
}

IRP_WEBVIEW_API unsigned long long irp_webview_geometry_revision(void) {
  // Called once per frame by the managed host: notice window size, display
  // scale and minimize changes here instead of subclassing Unity's window.
  if (host != nullptr && host->thread == GetCurrentThreadId()) Guarded([] {
      Maintain();
      return 1;
    });
  return geometryRevision.load(std::memory_order_relaxed);
}

IRP_WEBVIEW_API int irp_webview_get_bounds(const char *viewerKind, double *x, double *y, double *width,
                                           double *height, double *effectiveScale) {
  if (viewerKind == nullptr || x == nullptr || y == nullptr || width == nullptr || height == nullptr ||
      effectiveScale == nullptr || !OnGameThread())
    return 0;
  return Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface == nullptr || host->width <= 0 || host->height <= 0) return 0;
    const RectD &frame = surface->contentFrame;
    *x = frame.x / host->width;
    *y = frame.y / host->height;
    *width = frame.width / host->width;
    *height = frame.height / host->height;
    *effectiveScale = surface->effectiveScale;
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_get_selection_bounds(const char *viewerKind, double *x, double *y, double *width,
                                                     double *height, double *effectiveScale) {
  if (viewerKind == nullptr || x == nullptr || y == nullptr || width == nullptr || height == nullptr ||
      effectiveScale == nullptr || !OnGameThread())
    return 0;
  return Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface == nullptr || host->width <= 0 || host->height <= 0) return 0;
    RectD frame = surface->contentFrame;
    const RectD &measured = surface->selectionInViewport;
    if (!measured.Empty()) {
      const RectD &viewport = surface->webFrame;
      frame = Intersect(frame, {viewport.x + measured.x * viewport.width, viewport.y + measured.y * viewport.height,
                                measured.width * viewport.width, measured.height * viewport.height});
    }
    *x = frame.x / host->width;
    *y = frame.y / host->height;
    *width = frame.width / host->width;
    *height = frame.height / host->height;
    *effectiveScale = surface->effectiveScale;
    return 1;
  });
}

IRP_WEBVIEW_API int irp_webview_get_pointer(double *x, double *y) {
  if (x == nullptr || y == nullptr || !OnGameThread()) return 0;
  return Guarded([&] {
    if (host == nullptr || host->width <= 0 || host->height <= 0 || !IsWindow(host->window)) return 0;
    POINT point{};
    if (!GetCursorPos(&point) || !ScreenToClient(host->window, &point)) return 0;
    *x = (double)point.x / host->width;
    *y = (double)point.y / host->height;
    return 1;
  });
}

IRP_WEBVIEW_API void irp_webview_set_editing(int editing) {
  if (!OnGameThread()) return;
  Guarded([&] {
    if (host == nullptr) return 0;
    host->editing = editing != 0;
    for (auto &entry : host->surfaces) ApplyEditing(*entry.second);
    Layout();
    return 1;
  });
}

IRP_WEBVIEW_API void irp_webview_set_selected(const char *viewerKind, int selected) {
  if (viewerKind == nullptr || !OnGameThread()) return;
  Guarded([&] {
    Surface *surface = Lookup(viewerKind);
    if (surface != nullptr && surface->selected != (selected != 0)) {
      surface->selected = selected != 0;
      if (host->editing) {
        UpdateChrome();
        Commit();
      }
    }
    return 1;
  });
}

IRP_WEBVIEW_API void irp_webview_set_highlight(double red, double green, double blue) {
  // Retain the export for managed callers that resolve this ABI. The OBS page
  // owns its skin: a mod accent must never override per-key colors or alpha.
  (void)red;
  (void)green;
  (void)blue;
}

IRP_WEBVIEW_API void irp_webview_set_editor_exclusion(double x, double y, double width, double height) {
  if (!std::isfinite(x) || !std::isfinite(y) || !std::isfinite(width) || !std::isfinite(height) ||
      !OnGameThread())
    return;
  Guarded([&] {
    if (host == nullptr) return 0;
    RectD exclusion{x, y, std::max(0.0, width), std::max(0.0, height)};
    if (SameRect(exclusion, host->editorExclusion)) return 1;
    host->editorExclusion = exclusion;
    if (host->editing) Layout();
    return 1;
  });
}

IRP_WEBVIEW_API void irp_webview_dispose(void) {
  if (!OnGameThread()) return;
  Guarded([] {
    DestroyHost();
    ClearFaults();
    previewVisible = true;
    if (shared.messageWindow != nullptr) {
      KillTimer(shared.messageWindow, kRecoverTimer);
      SetTimer(shared.messageWindow, kIdleTimer, kIdleReleaseMs, nullptr);
    } else {
      ReleaseShared();
    }
    SetError("");
    return 1;
  });
}

IRP_WEBVIEW_API const char *irp_webview_last_error(void) {
  thread_local std::string result;
  std::lock_guard<std::mutex> lock(errorMutex);
  // A host problem affects every viewer, so it wins; otherwise report the
  // first viewer still failing, in stable identifier order.
  result = hostError;
  if (result.empty())
    for (const auto &fault : faults)
      if (!fault.second.empty()) {
        result = fault.second;
        break;
      }
  return result.c_str();
}

} // extern "C"
