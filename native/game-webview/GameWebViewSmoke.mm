// Standalone probe, not game/capture validation. Pass the dylib path as argv[1].
#import <AppKit/AppKit.h>
#import <WebKit/WebKit.h>

#include <arpa/inet.h>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <dlfcn.h>
#include <netinet/in.h>
#include <sys/select.h>
#include <sys/socket.h>
#include <thread>
#include <unistd.h>
#include <vector>

// These are the native class names in ADOFAI's installed UnityPlayer binary.
@interface PlayerWindow : NSWindow
@end
@implementation PlayerWindow
@end

@interface PlayerWindowView : NSView
@end
@implementation PlayerWindowView
- (BOOL)acceptsFirstResponder { return YES; }
@end

// Model a main window deterministically without activating this probe over the
// user's game. Native discovery must still reject a non-Unity main window.
@interface IRPProbeApplication : NSApplication
@property(nonatomic, weak) NSWindow *probeMainWindow;
@end
@implementation IRPProbeApplication
- (NSWindow *)mainWindow { return self.probeMainWindow ?: [super mainWindow]; }
@end

namespace {
class FixtureServer {
public:
  bool Start() {
    listener = socket(AF_INET, SOCK_STREAM, 0);
    if (listener < 0) return false;
    int enabled = 1;
    setsockopt(listener, SOL_SOCKET, SO_NOSIGPIPE, &enabled, sizeof(enabled));
    sockaddr_in address = {};
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
    if (bind(listener, reinterpret_cast<sockaddr *>(&address), sizeof(address)) != 0 ||
        listen(listener, 8) != 0) return false;
    socklen_t length = sizeof(address);
    if (getsockname(listener, reinterpret_cast<sockaddr *>(&address), &length) != 0) return false;
    port = ntohs(address.sin_port);
    worker = std::thread([this] {
      while (!stopped.load()) {
        fd_set ready;
        FD_ZERO(&ready);
        FD_SET(listener, &ready);
        timeval timeout = {0, 50000};
        if (select(listener + 1, &ready, nullptr, nullptr, &timeout) <= 0) continue;
        int client = accept(listener, nullptr, nullptr);
        if (client < 0) continue;
        int enabled = 1;
        setsockopt(client, SOL_SOCKET, SO_NOSIGPIPE, &enabled, sizeof(enabled));
        timeval receiveTimeout = {1, 0};
        setsockopt(client, SOL_SOCKET, SO_RCVTIMEO, &receiveTimeout, sizeof(receiveTimeout));
        char request[4096];
        ssize_t received = recv(client, request, sizeof(request), 0);
        if (received > 0) {
          const char *body = "<!doctype html><html><head><style>html,body{margin:0;"
                             "background:transparent}#fixture{width:80px;height:80px;"
                             "background:#28d9a1}</style></head><body><div id=fixture>"
                             "native fixture</div></body></html>";
          char response[2048];
          int size = snprintf(response, sizeof(response),
                              "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\n"
                              "Content-Length: %zu\r\nConnection: close\r\n\r\n%s",
                              strlen(body), body);
          send(client, response, size, 0);
          requests.fetch_add(1);
        }
        close(client);
      }
    });
    return true;
  }
  ~FixtureServer() {
    stopped.store(true);
    if (worker.joinable()) worker.join();
    if (listener >= 0) close(listener);
  }
  std::atomic<int> requests{0};
  uint16_t port = 0;

private:
  int listener = -1;
  std::atomic<bool> stopped{false};
  std::thread worker;
};

void Pump(double seconds) {
  NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:seconds];
  while (deadline.timeIntervalSinceNow > 0) {
    @autoreleasepool {
      [NSRunLoop.currentRunLoop runMode:NSDefaultRunLoopMode
                            beforeDate:[NSDate dateWithTimeIntervalSinceNow:0.01]];
    }
  }
}

void CollectWebViews(NSView *parent, std::vector<WKWebView *> &result) {
  if ([parent isKindOfClass:WKWebView.class]) {
    result.push_back((WKWebView *)parent);
    return;
  }
  for (NSView *child in parent.subviews) CollectWebViews(child, result);
}

std::vector<WKWebView *> WebViews(NSWindow *window) {
  std::vector<WKWebView *> result;
  CollectWebViews(window.contentView, result);
  return result;
}

bool Nearly(CGFloat left, CGFloat right) { return std::abs(left - right) < 0.5; }

struct Api {
  int (*supported)(void);
  int (*surface)(const char *, const char *, int, int);
  void (*visible)(int);
  void (*dispose)(void);
  const char *(*error)(void);
};

int Run(const char *path) {
  // The dynamic library remains loaded until process exit because the ObjC
  // runtime retains registered classes and WebKit may finish callbacks later.
  void *library = dlopen(path, RTLD_NOW | RTLD_LOCAL);
  if (library == nullptr) {
    fprintf(stderr, "FAIL: could not load native viewer dylib: %s\n", dlerror());
    return 1;
  }
  Api api = {
      reinterpret_cast<int (*)(void)>(dlsym(library, "irp_webview_supported")),
      reinterpret_cast<int (*)(const char *, const char *, int, int)>(dlsym(library, "irp_webview_set_surface")),
      reinterpret_cast<void (*)(int)>(dlsym(library, "irp_webview_set_visible")),
      reinterpret_cast<void (*)(void)>(dlsym(library, "irp_webview_dispose")),
      reinterpret_cast<const char *(*)(void)>(dlsym(library, "irp_webview_last_error")),
  };
  if (!api.supported || !api.surface || !api.visible || !api.dispose || !api.error) {
    fprintf(stderr, "FAIL: missing native viewer C ABI export\n");
    return 1;
  }
  auto check = [&](bool condition, const char *message) {
    if (!condition) fprintf(stderr, "FAIL: %s (%s)\n", message, api.error());
    return condition;
  };
  if (!check(api.supported() == 1, "native platform support")) return 1;
  if (!check(api.surface("hand", "https://example.com/?token=must-not-leak", 860, 540) == 0,
             "reject a non-loopback URL")) return 1;
  if (!check(strstr(api.error(), "must-not-leak") == nullptr && strlen(api.error()) < 256,
             "redacted bounded diagnostics")) return 1;

  NSWindow *other = [[NSWindow alloc] initWithContentRect:NSMakeRect(0, 0, 800, 600)
                                               styleMask:NSWindowStyleMaskTitled
                                                 backing:NSBackingStoreBuffered defer:NO];
  other.title = @"Unrelated native window";
  [other orderFront:nil];
  ((IRPProbeApplication *)NSApp).probeMainWindow = other;
  if (!check(NSApp.mainWindow == other, "prepare an unrelated main window")) return 1;
  if (!check(api.surface("hand", "http://127.0.0.1:1/fixture", 860, 540) == 0,
             "never fall back to an unrelated main window")) return 1;
  [other orderOut:nil];
  ((IRPProbeApplication *)NSApp).probeMainWindow = nil;

  FixtureServer server;
  if (!check(server.Start(), "start loopback HTTP fixture")) return 1;
  NSWindow *game = [[PlayerWindow alloc] initWithContentRect:NSMakeRect(40, 40, 1280, 720)
                                              styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskResizable
                                                backing:NSBackingStoreBuffered defer:NO];
  game.title = @"ImplResourcePack native host smoke probe";
  PlayerWindowView *gameView = [[PlayerWindowView alloc] initWithFrame:NSMakeRect(0, 0, 1280, 720)];
  game.contentView = gameView;
  [game orderFront:nil];
  [game makeFirstResponder:gameView];
  NSResponder *originalResponder = game.firstResponder;
  NSUInteger initialWindows = NSApp.windows.count;
  NSString *source = [NSString stringWithFormat:@"http://127.0.0.1:%u/fixture?token=smoke-only", server.port];
  if (!check(api.surface("hand", source.UTF8String, 860, 540) == 1 &&
             api.surface("foot", source.UTF8String, 640, 360) == 1,
             "attach both surfaces to Unity view")) return 1;
  auto views = WebViews(game);
  if (!check(views.size() == 2 && NSApp.windows.count == initialWindows,
             "two webviews in the same window, no extra NSWindow")) return 1;
  if (!check(game.firstResponder == originalResponder,
             "attachment preserves game first responder")) return 1;
  for (WKWebView *view : views) {
    if (!check([view.superview hitTest:NSMakePoint(20, 20)] == nil &&
               !view.acceptsFirstResponder && ![view becomeFirstResponder],
               "native content does not intercept input/focus")) return 1;
    if (!check(view.frame.size.width <= 430.01 && view.frame.size.height <= 270.01 &&
               Nearly(view.frame.origin.y, 16), "bounded bottom placement")) return 1;
    if (!check(![[view valueForKey:@"drawsBackground"] boolValue],
               "transparent WebKit background")) return 1;
  }
  NSDate *deadline = [NSDate dateWithTimeIntervalSinceNow:10];
  __block bool pageReady = false;
  while (!pageReady && deadline.timeIntervalSinceNow > 0) {
    [views[0] evaluateJavaScript:@"Boolean(document.getElementById('fixture'))"
              completionHandler:^(id result, NSError *error) {
                if (error == nil && [result boolValue]) pageReady = true;
              }];
    Pump(0.1);
  }
  if (!check(pageReady, "load local HTML in WKWebView")) return 1;
  __block bool viewportReady = false;
  [views[0] evaluateJavaScript:@"window.innerWidth"
            completionHandler:^(id result, NSError *error) {
              viewportReady = error == nil && Nearly([result doubleValue], 860);
            }];
  Pump(0.2);
  if (!check(viewportReady, "pageZoom preserves original CSS viewport")) return 1;

  int requests = server.requests.load();
  if (!check(api.surface("hand", source.UTF8String, 860, 540) == 1,
             "refresh the unchanged source")) return 1;
  Pump(0.2);
  if (!check(WebViews(game)[0] == views[0] && server.requests.load() == requests,
             "reuse existing webview and avoid URL reload")) return 1;
  [game setContentSize:NSMakeSize(640, 400)];
  Pump(0.1);
  views = WebViews(game);
  CGFloat rightEdge = 0;
  for (WKWebView *view : views) {
    rightEdge = std::max(rightEdge, NSMaxX(view.frame));
    if (!check(NSMaxX(view.frame) <= game.contentView.bounds.size.width - 15.5 &&
               view.frame.size.width <= 296.1 && Nearly(view.frame.origin.y, 16),
               "resize keeps surfaces within separate slots")) return 1;
  }
  if (!check(Nearly(rightEdge, 624), "foot surface follows right edge")) return 1;
  api.visible(0);
  for (WKWebView *view : views) {
    if (!check(view.hidden, "hide native preview")) return 1;
  }
  api.visible(1);
  if (!check(!views[0].hidden && !views[1].hidden, "show native preview")) return 1;

  [views[0] loadRequest:[NSURLRequest requestWithURL:[NSURL URLWithString:@"https://example.com/?token=must-not-leak"]]];
  Pump(0.2);
  if (!check(strstr(api.error(), "blocked navigation") != nullptr &&
             strstr(api.error(), "must-not-leak") == nullptr,
             "navigation delegate rejects an external origin without token disclosure")) return 1;
  if (!check(api.surface("foot", nullptr, 0, 0) == 1 && WebViews(game).size() == 1,
             "remove one viewer independently")) return 1;
  api.dispose();
  Pump(0.1);
  if (!check(WebViews(game).empty() && game.firstResponder == originalResponder,
             "dispose detaches native views and preserves focus")) return 1;
  if (!check(api.surface("hand", source.UTF8String, 860, 540) == 1,
             "reattach after mod-style dispose")) return 1;
  api.dispose();
  [game orderOut:nil];
  printf("PASS: native C ABI, window selection, local HTML, CSS viewport, transparency, "
         "input pass-through, reuse, resize, visibility, navigation, removal, reload.\n");
  printf("Discord/OBS capture and the real Unity render hierarchy require manual game verification.\n");
  return 0;
}
} // namespace

int main(int argc, char **argv) {
  @autoreleasepool {
    if (argc != 2) {
      fprintf(stderr, "Usage: GameWebViewSmoke /absolute/path/libImplGameWebView.dylib\n");
      return 2;
    }
    [IRPProbeApplication sharedApplication];
    [NSApp setActivationPolicy:NSApplicationActivationPolicyAccessory];
    [NSApp finishLaunching];
    @try {
      return Run(argv[1]);
    } @catch (NSException *) {
      fprintf(stderr, "FAIL: macOS exception in native viewer smoke probe\n");
      return 1;
    }
  }
}
