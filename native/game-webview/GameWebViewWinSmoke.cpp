// Standalone probe for ImplGameWebView.dll. It stands in for ADOFAI: a
// UnityWndClass window presenting a D3D11 flip-model swap chain, driven through
// the same exported ABI the mod loads at runtime. A loopback HTTP fixture plays
// the ImplDmNote OBS page. Results are checked from composed screen pixels, so
// run it on an interactive desktop:
//   ImplGameWebViewSmoke.exe <dll> <log> [screenshot directory]

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#include <d3d11.h>
#include <dxgi1_2.h>
#include <shellapi.h>
#include <tlhelp32.h>
#include <wrl.h>

#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdarg>
#include <cstdio>
#include <functional>
#include <string>
#include <thread>

using Microsoft::WRL::ComPtr;

namespace {
FILE *logFile = stdout;
int failures = 0;

void Log(const char *format, ...) {
  va_list args;
  va_start(args, format);
  vfprintf(logFile, format, args);
  va_end(args);
  fputc('\n', logFile);
  fflush(logFile);
}

void Check(bool condition, const char *name) {
  Log("%s %s", condition ? "PASS" : "FAIL", name);
  if (!condition) ++failures;
}

// ---- ABI, resolved exactly like the managed host does.
struct Api {
  int (*supported)();
  int (*setSurface)(const char *, const char *, int, int);
  void (*setVisible)(int);
  int (*setLayout)(const char *, int, double, double, double);
  int (*setBounds)(const char *, double, double, double, double, double);
  int (*getReferenceSize)(const char *, double *, double *);
  unsigned long long (*geometryRevision)();
  int (*getBounds)(const char *, double *, double *, double *, double *, double *);
  int (*getSelectionBounds)(const char *, double *, double *, double *, double *, double *);
  int (*getPointer)(double *, double *);
  void (*setEditing)(int);
  void (*setSelected)(const char *, int);
  void (*setHighlight)(double, double, double);
  void (*setEditorExclusion)(double, double, double, double);
  void (*dispose)();
  const char *(*lastError)();
} api;

template <class T> bool Resolve(HMODULE module, const char *name, T *target) {
  *target = reinterpret_cast<T>(GetProcAddress(module, name));
  if (*target == nullptr) Log("FAIL missing export %s", name);
  return *target != nullptr;
}

bool LoadApi(const wchar_t *path) {
  HMODULE module = LoadLibraryW(path);
  if (module == nullptr) {
    Log("FAIL could not load the native viewer library (%lu)", GetLastError());
    return false;
  }
  return Resolve(module, "irp_webview_supported", &api.supported) &&
         Resolve(module, "irp_webview_set_surface", &api.setSurface) &&
         Resolve(module, "irp_webview_set_visible", &api.setVisible) &&
         Resolve(module, "irp_webview_set_layout", &api.setLayout) &&
         Resolve(module, "irp_webview_set_bounds", &api.setBounds) &&
         Resolve(module, "irp_webview_get_reference_size", &api.getReferenceSize) &&
         Resolve(module, "irp_webview_geometry_revision", &api.geometryRevision) &&
         Resolve(module, "irp_webview_get_bounds", &api.getBounds) &&
         Resolve(module, "irp_webview_get_selection_bounds", &api.getSelectionBounds) &&
         Resolve(module, "irp_webview_get_pointer", &api.getPointer) &&
         Resolve(module, "irp_webview_set_editing", &api.setEditing) &&
         Resolve(module, "irp_webview_set_selected", &api.setSelected) &&
         Resolve(module, "irp_webview_set_highlight", &api.setHighlight) &&
         Resolve(module, "irp_webview_set_editor_exclusion", &api.setEditorExclusion) &&
         Resolve(module, "irp_webview_dispose", &api.dispose) &&
         Resolve(module, "irp_webview_last_error", &api.lastError);
}

// ---- Loopback fixture standing in for ImplDmNote's OBS page.
const char *kPage = R"HTML(<!doctype html>
<html><head><meta charset="utf-8"><style>
html, body { margin: 0; background: transparent; overflow: hidden; }
#root { position: absolute; inset: 0; }
.key { position: absolute; background: #ff0000; }
</style></head><body>
<div id="root" data-dmn-viewer-width="400" data-dmn-viewer-height="200" data-dmn-viewer-padding="10">
  <div class="key" data-key-element style="left: 10px; top: 110px; width: 380px; height: 80px"></div>
</div>
<script>
const query = new URLSearchParams(location.search);
if (query.get('case') === 'grow') setTimeout(() => {
  const root = document.getElementById('root');
  root.querySelector('.key').style.width = '580px';
  root.setAttribute('data-dmn-viewer-width', '600');
}, 1500);
if (query.get('case') === 'escape') setTimeout(() => { location.href = 'http://example.com/'; }, 300);
</script></body></html>)HTML";

std::atomic<int> requests{0};

void Serve(SOCKET listener) {
  for (;;) {
    SOCKET client = accept(listener, nullptr, nullptr);
    if (client == INVALID_SOCKET) return;
    char buffer[4096];
    int received = recv(client, buffer, sizeof(buffer) - 1, 0);
    if (received > 0) {
      buffer[received] = '\0';
      ++requests;
      bool page = std::strncmp(buffer, "GET /?", 6) == 0 || std::strncmp(buffer, "GET / ", 6) == 0;
      std::string body = page ? kPage : "not found";
      std::string response = std::string(page ? "HTTP/1.1 200 OK\r\n" : "HTTP/1.1 404 Not Found\r\n") +
                             "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\n"
                             "Connection: close\r\nContent-Length: " +
                             std::to_string(body.size()) + "\r\n\r\n" + body;
      send(client, response.data(), (int)response.size(), 0);
    }
    closesocket(client);
  }
}

int StartServer() {
  WSADATA data;
  WSAStartup(MAKEWORD(2, 2), &data);
  SOCKET listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
  sockaddr_in address{};
  address.sin_family = AF_INET;
  address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
  if (bind(listener, reinterpret_cast<sockaddr *>(&address), sizeof(address)) != 0 || listen(listener, 16) != 0)
    return 0;
  int length = sizeof(address);
  getsockname(listener, reinterpret_cast<sockaddr *>(&address), &length);
  std::thread(Serve, listener).detach();
  return ntohs(address.sin_port);
}

// ---- A Unity-like game window.
HWND window = nullptr;
ComPtr<ID3D11Device> device;
ComPtr<ID3D11DeviceContext> context;
ComPtr<IDXGISwapChain1> swapChain;
ComPtr<ID3D11RenderTargetView> target;
std::atomic<int> clicks{0};
const float kClear[4] = {0.1f, 0.2f, 0.7f, 1.0f};

LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) {
  switch (message) {
  case WM_LBUTTONDOWN:
    ++clicks;
    return 0;
  case WM_SIZE:
    if (swapChain && wParam != SIZE_MINIMIZED) {
      target.Reset();
      swapChain->ResizeBuffers(0, 0, 0, DXGI_FORMAT_UNKNOWN, 0);
    }
    return 0;
  case WM_CLOSE:
    return 0;
  }
  return DefWindowProcW(hwnd, message, wParam, lParam);
}

bool CreateGameWindow(int width, int height) {
  WNDCLASSEXW windowClass{sizeof(windowClass)};
  windowClass.lpfnWndProc = WindowProc;
  windowClass.hInstance = GetModuleHandleW(nullptr);
  windowClass.hCursor = LoadCursor(nullptr, IDC_ARROW);
  windowClass.lpszClassName = L"UnityWndClass";
  RegisterClassExW(&windowClass);
  RECT rect{0, 0, width, height};
  AdjustWindowRectEx(&rect, WS_OVERLAPPEDWINDOW, FALSE, WS_EX_TOPMOST);
  window = CreateWindowExW(WS_EX_TOPMOST, windowClass.lpszClassName, L"ImplGameWebView smoke", WS_OVERLAPPEDWINDOW,
                           40, 40, rect.right - rect.left, rect.bottom - rect.top, nullptr, nullptr,
                           windowClass.hInstance, nullptr);
  if (window == nullptr) return false;
  ShowWindow(window, SW_SHOW);
  SetForegroundWindow(window);
  if (FAILED(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &device,
                               nullptr, &context)))
    return false;
  ComPtr<IDXGIDevice> dxgi;
  ComPtr<IDXGIAdapter> adapter;
  ComPtr<IDXGIFactory2> factory;
  device.As(&dxgi);
  dxgi->GetAdapter(&adapter);
  adapter->GetParent(IID_PPV_ARGS(&factory));
  DXGI_SWAP_CHAIN_DESC1 description{};
  description.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
  description.SampleDesc.Count = 1;
  description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
  description.BufferCount = 2;
  description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
  return SUCCEEDED(factory->CreateSwapChainForHwnd(device.Get(), window, &description, nullptr, nullptr, &swapChain));
}

void RenderFrame() {
  if (!target) {
    ComPtr<ID3D11Texture2D> buffer;
    if (FAILED(swapChain->GetBuffer(0, IID_PPV_ARGS(&buffer)))) return;
    device->CreateRenderTargetView(buffer.Get(), nullptr, &target);
  }
  context->OMSetRenderTargets(1, target.GetAddressOf(), nullptr);
  context->ClearRenderTargetView(target.Get(), kClear);
  swapChain->Present(1, 0);
}

// Pumps messages and renders like Unity's player loop, polling the revision
// once per frame as the managed host does.
bool Pump(int milliseconds, const std::function<bool()> &done = nullptr) {
  auto deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(milliseconds);
  while (std::chrono::steady_clock::now() < deadline) {
    MSG message;
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE)) {
      TranslateMessage(&message);
      DispatchMessageW(&message);
    }
    if (!IsIconic(window)) RenderFrame();
    else Sleep(16);
    api.geometryRevision();
    if (done && done()) return true;
  }
  return done ? done() : true;
}

SIZE ClientSize() {
  RECT rect{};
  GetClientRect(window, &rect);
  return {rect.right, rect.bottom};
}

COLORREF Pixel(double normalizedX, double normalizedY) {
  SIZE size = ClientSize();
  POINT point{(LONG)std::lround(normalizedX * size.cx), (LONG)std::lround(normalizedY * size.cy)};
  ClientToScreen(window, &point);
  HDC screen = GetDC(nullptr);
  COLORREF color = GetPixel(screen, point.x, point.y);
  ReleaseDC(nullptr, screen);
  return color;
}

// Saves the composed client area for visual inspection next to the log.
std::wstring shotDirectory;
void Screenshot(const wchar_t *name) {
  if (shotDirectory.empty()) return;
  SIZE size = ClientSize();
  POINT origin{0, 0};
  ClientToScreen(window, &origin);
  HDC screen = GetDC(nullptr);
  HDC memory = CreateCompatibleDC(screen);
  BITMAPINFO info{};
  info.bmiHeader.biSize = sizeof(info.bmiHeader);
  info.bmiHeader.biWidth = size.cx;
  info.bmiHeader.biHeight = size.cy;
  info.bmiHeader.biPlanes = 1;
  info.bmiHeader.biBitCount = 24;
  void *bits = nullptr;
  HBITMAP bitmap = CreateDIBSection(screen, &info, DIB_RGB_COLORS, &bits, nullptr, 0);
  HGDIOBJ previous = SelectObject(memory, bitmap);
  BitBlt(memory, 0, 0, size.cx, size.cy, screen, origin.x, origin.y, SRCCOPY);
  SelectObject(memory, previous);
  DWORD stride = ((size.cx * 3 + 3) / 4) * 4, bytes = stride * size.cy;
  BITMAPFILEHEADER header{0x4d42, sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER) + bytes, 0, 0,
                          sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER)};
  std::wstring path = shotDirectory + L"\\" + name + L".bmp";
  FILE *file = nullptr;
  if (_wfopen_s(&file, path.c_str(), L"wb") == 0 && file) {
    fwrite(&header, sizeof(header), 1, file);
    fwrite(&info.bmiHeader, sizeof(info.bmiHeader), 1, file);
    fwrite(bits, 1, bytes, file);
    fclose(file);
  }
  DeleteObject(bitmap);
  DeleteDC(memory);
  ReleaseDC(nullptr, screen);
}

bool Near(COLORREF color, int red, int green, int blue) {
  return std::abs(GetRValue(color) - red) <= 16 && std::abs(GetGValue(color) - green) <= 16 &&
         std::abs(GetBValue(color) - blue) <= 16;
}

bool IsKey(COLORREF color) { return Near(color, 255, 0, 0); }
bool IsGame(COLORREF color) { return Near(color, 26, 51, 178); }

void LogPixel(const char *name, COLORREF color) {
  Log("  %s = rgb(%d,%d,%d)", name, GetRValue(color), GetGValue(color), GetBValue(color));
}

struct Bounds {
  double x = 0, y = 0, width = 0, height = 0, scale = 0;
};

Bounds GetBounds(const char *kind, bool selection = false) {
  Bounds bounds;
  (selection ? api.getSelectionBounds : api.getBounds)(kind, &bounds.x, &bounds.y, &bounds.width, &bounds.height,
                                                        &bounds.scale);
  return bounds;
}

bool ReferenceIs(const char *kind, double width, double height) {
  double w = 0, h = 0;
  return api.getReferenceSize(kind, &w, &h) && std::abs(w - width) < 0.01 && std::abs(h - height) < 0.01;
}

DWORD FindChildProcess(DWORD parent, const wchar_t *name) {
  HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
  PROCESSENTRY32W entry{sizeof(entry)};
  DWORD found = 0;
  for (BOOL more = Process32FirstW(snapshot, &entry); more; more = Process32NextW(snapshot, &entry))
    if (entry.th32ParentProcessID == parent && _wcsicmp(entry.szExeFile, name) == 0) found = entry.th32ProcessID;
  CloseHandle(snapshot);
  return found;
}

std::string Describe(HWND described) {
  if (described == nullptr) return "none";
  wchar_t name[64] = {};
  GetClassNameW(described, name, 64);
  DWORD process = 0;
  GetWindowThreadProcessId(described, &process);
  char text[160];
  std::snprintf(text, sizeof(text), "%ls (pid %lu%s)", name, process,
                process == GetCurrentProcessId() ? ", this process" : "");
  return text;
}

BOOL CALLBACK LogChild(HWND child, LPARAM) {
  RECT rect{};
  GetWindowRect(child, &rect);
  Log("  child window: %s visible=%d size=%ldx%ld", Describe(child).c_str(), IsWindowVisible(child) ? 1 : 0,
      rect.right - rect.left, rect.bottom - rect.top);
  return TRUE;
}

UINT Click(double normalizedX, double normalizedY) {
  SIZE size = ClientSize();
  POINT point{(LONG)std::lround(normalizedX * size.cx), (LONG)std::lround(normalizedY * size.cy)};
  ClientToScreen(window, &point);
  SetCursorPos(point.x, point.y);
  INPUT inputs[2]{};
  inputs[0].type = inputs[1].type = INPUT_MOUSE;
  inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
  inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
  return SendInput(2, inputs, sizeof(INPUT));
}

double Seconds(std::chrono::steady_clock::time_point start) {
  return std::chrono::duration<double>(std::chrono::steady_clock::now() - start).count();
}
} // namespace

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int) {
  int argc = 0;
  wchar_t **argv = CommandLineToArgvW(GetCommandLineW(), &argc);
  if (argc >= 3) _wfopen_s(&logFile, argv[2], L"w");
  if (argc >= 4) shotDirectory = argv[3];
  if (logFile == nullptr) logFile = stdout;
  SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
  CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

  if (argc < 2 || !LoadApi(argv[1])) return 2;
  int port = StartServer();
  if (port == 0 || !CreateGameWindow(1280, 720)) {
    Log("FAIL could not prepare the fixture window or server");
    return 2;
  }
  Pump(300);
  char base[128];
  std::snprintf(base, sizeof(base), "http://127.0.0.1:%d/?token=smoke&viewer=", port);
  std::string handUrl = std::string(base) + "hand&case=basic";
  std::string footUrl = std::string(base) + "foot&case=grow";

  Check(api.supported() == 1, "WebView2 Runtime is available");
  Check(api.setSurface("hand", "https://127.0.0.1:1/", 400, 200) == 0, "rejects non-loopback HTTP sources");
  Check(api.setSurface("other", handUrl.c_str(), 400, 200) == 0, "rejects unknown viewer identifiers");

  auto start = std::chrono::steady_clock::now();
  Check(api.setSurface("hand", handUrl.c_str(), 860, 320) == 1, "attaches a viewer");
  Log("  first attach call took %.1f ms", Seconds(start) * 1000);
  Check(ReferenceIs("hand", 430, 160), "descriptor bootstraps the fitted reference size");
  unsigned long long revision = api.geometryRevision();
  bool laidOut = Pump(20000, [] { return ReferenceIs("hand", 190, 90); });
  Check(laidOut, "page layout replaces the descriptor size (cropped padding)");
  Log("  page reported its layout after %.2f s; last error: '%s'", Seconds(start), api.lastError());
  Check(api.geometryRevision() > revision, "page layout bumps the geometry revision");

  // Default placement: bottom-left, 16 DIPs inset.
  Bounds automatic = GetBounds("hand");
  Log("  automatic bounds %.4f %.4f %.4f %.4f scale %.3f", automatic.x, automatic.y, automatic.width,
      automatic.height, automatic.scale);
  Check(automatic.width > 0 && automatic.x < 0.1 && automatic.y + automatic.height > 0.9,
        "automatic placement anchors the hand viewer bottom-left");

  // Explicit layout as the managed editor sends it.
  SIZE size = ClientSize();
  double width = 0.3, height = width * size.cx * (180.0 / 380.0) / size.cy;
  Check(api.setBounds("hand", 0.1, 0.4, width, height, 1.25) == 1, "accepts explicit normalized bounds");
  Bounds placed = GetBounds("hand");
  Check(std::abs(placed.x - 0.1) < 1e-6 && std::abs(placed.width - width) < 1e-6 && placed.scale == 1.25,
        "reports explicit bounds unchanged");
  Pump(800);
  Screenshot(L"placed");
  // The key fills the lower 80 of 180 cropped CSS pixels (100..180 after the 10px padding).
  double keyX = 0.1 + width * 0.5, keyY = 0.4 + height * (140.0 / 180.0);
  double clearY = 0.4 + height * (30.0 / 180.0);
  COLORREF key = Pixel(keyX, keyY), transparent = Pixel(keyX, clearY), outside = Pixel(0.8, 0.2);
  LogPixel("key", key);
  LogPixel("transparent page area", transparent);
  LogPixel("outside", outside);
  Check(IsKey(key), "page content composites inside the game window");
  Check(IsGame(transparent), "page background is transparent over the game");
  Check(IsGame(outside), "game pixels outside the viewer are untouched");

  EnumChildWindows(window, LogChild, 0);
  SetForegroundWindow(window);
  Pump(200);
  Log("  foreground before click: %s", Describe(GetForegroundWindow()).c_str());
  int before = clicks;
  UINT sent = Click(keyX, keyY);
  Pump(300);
  Log("  SendInput sent %u (error %lu); window under cursor: %s", sent, sent ? 0 : GetLastError(),
      Describe(WindowFromPoint([&] {
        POINT point{};
        GetCursorPos(&point);
        return point;
      }())).c_str());
  Log("  foreground after click: %s", Describe(GetForegroundWindow()).c_str());
  Check(clicks == before + 1, "clicks over the viewer reach the game window");
  HWND foreground = GetForegroundWindow();
  DWORD owner = 0;
  GetWindowThreadProcessId(foreground, &owner);
  Check(foreground == window || owner != FindChildProcess(GetCurrentProcessId(), L"msedgewebview2.exe"),
        "the viewer never takes foreground focus");
  Log("  keyboard focus: %s", Describe(GetFocus()).c_str());
  Check(GetFocus() == window, "keyboard focus stays on the game window");

  // Editing chrome and selection.
  api.setEditing(1);
  api.setSelected("hand", 1);
  Pump(800);
  Bounds selection = GetBounds("hand", true);
  Log("  selection %.4f %.4f %.4f %.4f", selection.x, selection.y, selection.width, selection.height);
  Check(selection.height > 0 && selection.height < placed.height * 0.6 && selection.y > placed.y,
        "selection bounds cover the visible keys only");
  Screenshot(L"editing");
  // The 3px stroke is inset 1.5px from the content frame's top edge.
  int white = 0, samples = 0;
  for (int i = 0; i < 60; ++i, ++samples)
    if (Near(Pixel(placed.x + placed.width * (0.2 + i * 0.01), placed.y + 1.5 / size.cy), 255, 255, 255)) ++white;
  Log("  outline samples white: %d / %d", white, samples);
  Check(white > samples / 4 && white < samples, "editing draws the dashed outline");

  api.setEditorExclusion(keyX - 0.02, keyY - 0.02, 0.04, 0.04);
  Pump(600);
  COLORREF cut = Pixel(keyX, keyY), kept = Pixel(0.1 + width * 0.1, keyY);
  LogPixel("excluded key", cut);
  Check(IsGame(cut), "editor exclusion cuts the page");
  Check(IsKey(kept), "editor exclusion keeps the rest of the page");
  api.setEditorExclusion(0, 0, 0, 0);
  api.setSelected("hand", 0);
  api.setEditing(0);
  Pump(600);
  Check(IsKey(Pixel(keyX, keyY)), "clearing the exclusion restores the page");

  api.setVisible(0);
  Pump(400);
  Check(IsGame(Pixel(keyX, keyY)), "hiding removes the viewer");
  api.setVisible(1);
  Pump(400);
  Check(IsKey(Pixel(keyX, keyY)), "showing restores the viewer without a reload");

  // Content growth and stale descriptors.
  Check(api.setSurface("foot", footUrl.c_str(), 860, 320) == 1, "attaches a second viewer");
  Check(Pump(15000, [] { return ReferenceIs("foot", 190, 90); }), "second viewer reports its layout");
  Check(Pump(5000, [] { return ReferenceIs("foot", 290, 90); }), "content growth updates the reference size");
  Check(api.setSurface("foot", footUrl.c_str(), 860, 320) == 1 && ReferenceIs("foot", 290, 90),
        "a repeated descriptor does not restore stale desktop bounds");
  Bounds foot = GetBounds("foot");
  Check(foot.x + foot.width > 0.9, "automatic placement anchors the foot viewer bottom-right");

  // Window size changes.
  revision = api.geometryRevision();
  SetWindowPos(window, nullptr, 0, 0, 1600, 900, SWP_NOMOVE | SWP_NOZORDER);
  Pump(600);
  Check(api.geometryRevision() > revision, "window resize bumps the geometry revision");
  Bounds resized = GetBounds("hand");
  Check(std::abs(resized.x - 0.1) < 1e-6 && std::abs(resized.width - width) < 1e-6,
        "explicit bounds stay normalized after a resize");
  Pump(600);
  Check(IsKey(Pixel(keyX, keyY)), "viewer follows the resized window");

  ShowWindow(window, SW_MINIMIZE);
  Pump(600);
  ShowWindow(window, SW_RESTORE);
  SetForegroundWindow(window);
  Pump(1000);
  Check(IsKey(Pixel(keyX, keyY)), "viewer returns after minimize and restore");

  // Navigation stays on the descriptor's origin.
  std::string escape = std::string(base) + "hand&case=escape";
  api.setSurface("hand", escape.c_str(), 860, 320);
  bool blocked = Pump(8000, [] { return std::strstr(api.lastError(), "blocked navigation") != nullptr; });
  Check(blocked, "navigation outside the local source is blocked");
  // Faults belong to one viewer: another page loading must not clear this one.
  std::string footReload = footUrl + "&reload=1";
  api.setSurface("foot", footReload.c_str(), 860, 320);
  Pump(3000);
  Check(std::strstr(api.lastError(), "blocked navigation") != nullptr,
        "another viewer's page load keeps this viewer's fault");
  api.setSurface("hand", handUrl.c_str(), 860, 320);
  Pump(3000, [] { return ReferenceIs("hand", 190, 90); });
  Check(api.lastError()[0] == '\0', "reloading the viewer clears its fault");

  // A crashed browser process rebuilds the environment and pages.
  DWORD browser = FindChildProcess(GetCurrentProcessId(), L"msedgewebview2.exe");
  Check(browser != 0, "WebView2 runs in its own browser process");
  if (browser != 0) {
    HANDLE process = OpenProcess(PROCESS_TERMINATE, FALSE, browser);
    TerminateProcess(process, 1);
    CloseHandle(process);
    start = std::chrono::steady_clock::now();
    Pump(1500);
    bool recovered = Pump(20000, [&] { return IsKey(Pixel(keyX, keyY)); });
    Check(recovered, "viewers recover after the browser process exits");
    Log("  recovered after %.2f s", Seconds(start));
  }

  // Dispose and a warm reattach, as when gameplay pauses and resumes.
  api.dispose();
  Pump(400);
  Check(IsGame(Pixel(keyX, keyY)), "dispose removes every viewer");
  start = std::chrono::steady_clock::now();
  api.setSurface("hand", handUrl.c_str(), 860, 320);
  api.setBounds("hand", 0.1, 0.4, width, height, 1.25);
  bool warm = Pump(10000, [&] { return IsKey(Pixel(keyX, keyY)); });
  Check(warm, "a disposed host reattaches");
  Log("  warm reattach visible after %.2f s", Seconds(start));
  api.dispose();
  Pump(200);

  Log("requests served: %d", requests.load());
  Log("%s: %d failure(s)", failures == 0 ? "OK" : "FAILED", failures);
  return failures == 0 ? 0 : 1;
}
