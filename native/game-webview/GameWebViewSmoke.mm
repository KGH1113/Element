// Standalone probe, not game/capture validation. Pass the dylib path as argv[1].
#import <AppKit/AppKit.h>
#import <QuartzCore/QuartzCore.h>
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
                             "position:relative;background:rgba(40,217,161,.25);color:#39ff14}"
                             ":root{--graph-color:#123456}</style></head><body><div id=root><div id=fixture data-state=inactive>"
                             "<span id=label>native fixture</span>"
                             "<div id=transparent data-state=active style='position:absolute;left:0;top:0;width:20px;height:20px;"
                             "background:rgba(1,2,3,0);color:rgba(4,5,6,0)'>"
                             "<span id=transparentLabel>hidden key</span></div></div></div><script>window.api={settings:{"
                             "get:()=>Promise.resolve({gridSettings:{overlayPadding:0}}),"
                             "onChanged:fn=>{window.fixturePadding=fn}}};</script></body></html>";
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
  int (*layout)(const char *, int, double, double, double);
  int (*setBounds)(const char *, double, double, double, double, double);
  int (*referenceSize)(const char *, double *, double *);
  int (*contentSize)(const char *, double *, double *);
  int (*viewSize)(double *, double *);
  unsigned long long (*geometryRevision)(void);
  int (*bounds)(const char *, double *, double *, double *, double *, double *);
  int (*selectionBounds)(const char *, double *, double *, double *, double *, double *);
  int (*pointer)(double *, double *);
  void (*editing)(int);
  void (*selected)(const char *, int);
  void (*highlight)(double, double, double);
  void (*exclusion)(double, double, double, double);
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
      reinterpret_cast<int (*)(const char *, int, double, double, double)>(dlsym(library, "irp_webview_set_layout")),
      reinterpret_cast<int (*)(const char *, double, double, double, double, double)>(dlsym(library, "irp_webview_set_bounds")),
      reinterpret_cast<int (*)(const char *, double *, double *)>(dlsym(library, "irp_webview_get_reference_size")),
      reinterpret_cast<int (*)(const char *, double *, double *)>(dlsym(library, "irp_webview_get_content_size")),
      reinterpret_cast<int (*)(double *, double *)>(dlsym(library, "irp_webview_get_view_size")),
      reinterpret_cast<unsigned long long (*)(void)>(dlsym(library, "irp_webview_geometry_revision")),
      reinterpret_cast<int (*)(const char *, double *, double *, double *, double *, double *)>(dlsym(library, "irp_webview_get_bounds")),
      reinterpret_cast<int (*)(const char *, double *, double *, double *, double *, double *)>(dlsym(library, "irp_webview_get_selection_bounds")),
      reinterpret_cast<int (*)(double *, double *)>(dlsym(library, "irp_webview_get_pointer")),
      reinterpret_cast<void (*)(int)>(dlsym(library, "irp_webview_set_editing")),
      reinterpret_cast<void (*)(const char *, int)>(dlsym(library, "irp_webview_set_selected")),
      reinterpret_cast<void (*)(double, double, double)>(dlsym(library, "irp_webview_set_highlight")),
      reinterpret_cast<void (*)(double, double, double, double)>(dlsym(library, "irp_webview_set_editor_exclusion")),
      reinterpret_cast<void (*)(void)>(dlsym(library, "irp_webview_dispose")),
      reinterpret_cast<const char *(*)(void)>(dlsym(library, "irp_webview_last_error")),
  };
  if (!api.supported || !api.surface || !api.visible || !api.layout || !api.setBounds || !api.referenceSize || !api.contentSize || !api.viewSize || !api.geometryRevision || !api.bounds || !api.selectionBounds || !api.pointer || !api.editing || !api.selected || !api.highlight || !api.exclusion || !api.dispose || !api.error) {
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
  game.title = @"Element native host smoke probe";
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
  // Test a host offset from the drawable without moving the user's pointer.
  // Both zero and nonzero offsets must map back to its real desktop position.
  NSView *pointerHost = views[0].superview;
  for (CGFloat offset : {0.0, 28.0}) {
    [pointerHost setFrameOrigin:NSMakePoint(0, offset)];
    double px, py;
    NSPoint before = NSEvent.mouseLocation;
    if (!check(api.pointer(&px, &py) == 1, "read pointer in native host coordinates")) return 1;
    NSPoint local = NSMakePoint(px * pointerHost.bounds.size.width, (1 - py) * pointerHost.bounds.size.height);
    NSPoint actual = [game convertPointToScreen:[pointerHost convertPoint:local toView:nil]];
    NSPoint after = NSEvent.mouseLocation;
    if (!check(actual.x >= std::min(before.x, after.x) - 1 && actual.x <= std::max(before.x, after.x) + 1 &&
               actual.y >= std::min(before.y, after.y) - 1 && actual.y <= std::max(before.y, after.y) + 1,
               "pointer follows the native host origin independently of Unity drawable/titlebar offsets")) return 1;
  }
  [pointerHost setFrameOrigin:NSZeroPoint];
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
               Nearly(view.frame.size.width, 430) && Nearly(view.frame.origin.y, 16),
               "window resize preserves CSS pixel size instead of fitting separate slots")) return 1;
  }
  if (!check(Nearly(rightEdge, 624), "foot surface follows right edge")) return 1;
  api.visible(0);
  for (WKWebView *view : views) {
    if (!check(view.hidden, "hide native preview")) return 1;
  }
  api.visible(1);
  if (!check(!views[0].hidden && !views[1].hidden, "show native preview")) return 1;

  double x, y, width, height, scale;
  if (!check(api.layout("hand", 1, 0.5, 0.4, 0.6) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1,
             "set and inspect normalized layout")) return 1;
  if (!check(std::abs(x + width / 2 - 0.5) < 0.0001 &&
             std::abs(y + height / 2 - 0.4) < 0.0001 &&
             std::abs(scale - 0.6) < 0.0001 &&
             std::abs((width * 640) / (height * 400) - 860.0 / 540) < 0.0001,
             "top-left normalized center and uniform aspect ratio")) return 1;
  api.editing(1);
  Pump(0.2);
  NSString *skinCheck = @"(()=>{const s=id=>getComputedStyle(document.getElementById(id));"
    "return s('fixture').backgroundColor==='rgba(40, 217, 161, 0.25)'"
    "&& s('label').color==='rgb(57, 255, 20)'"
    "&& s('transparent').backgroundColor==='rgba(1, 2, 3, 0)'"
    "&& s('transparentLabel').color==='rgba(4, 5, 6, 0)'"
    "&& getComputedStyle(document.documentElement).getPropertyValue('--graph-color').trim()==='#123456';})()";
  auto skinPreserved = [&]() {
    __block bool preserved = false;
    [views[0] evaluateJavaScript:skinCheck completionHandler:^(id result, NSError *error) {
      preserved = error == nil && [result isKindOfClass:NSNumber.class] && [result boolValue];
    }];
    Pump(.2);
    return preserved;
  };
  if (!check(skinPreserved(), "page load preserves key colors, alpha, transparent layers and graph color")) return 1;
  api.highlight(.1, .5, .9);
  if (!check(skinPreserved() && server.requests.load() == requests,
             "mod highlight changes preserve the OBS skin without page reload")) return 1;
  [views[0] evaluateJavaScript:@"document.getElementById('fixture').dataset.state='active';"
    "document.getElementById('transparent').dataset.state='inactive';" completionHandler:nil];
  api.highlight(.9, .2, .1);
  if (!check(skinPreserved(), "state changes preserve user text and transparent overlay colors")) return 1;
  double sx, sy, sw, sh, ss;
  if (!check(api.selectionBounds("hand", &sx, &sy, &sw, &sh, &ss) == 1 &&
             sw < width && sh < height && Nearly(sw * 640, 80 * views[0].pageZoom) &&
             Nearly(sh * 400, 80 * views[0].pageZoom),
             "unselected target tightly follows visible keys instead of maximum note viewport")) return 1;
  [views[0] evaluateJavaScript:@"document.getElementById('fixture').style.cssText='transform:translate(100px,180px);width:120px;height:60px'" completionHandler:nil];
  Pump(0.2);
  if (!check(api.selectionBounds("hand", &sx, &sy, &sw, &sh, &ss) == 1 &&
             Nearly((sx - x) * 640, 100 * views[0].pageZoom) &&
             Nearly((sy - y) * 400, 180 * views[0].pageZoom) &&
             Nearly(sw * 640, 120 * views[0].pageZoom) && Nearly(sh * 400, 60 * views[0].pageZoom),
             "selection geometry tracks changed key position and size while editing")) return 1;
  [views[0] evaluateJavaScript:@"document.getElementById('fixture').style.cssText=''" completionHandler:nil];
  NSView *chrome = views[0].superview.subviews.lastObject;
  if (!check([NSStringFromClass(chrome.class) isEqualToString:@"IRPGameWebViewChrome"] &&
             !chrome.hidden && [chrome hitTest:NSMakePoint(320, 160)] == nil,
             "editing chrome draws above WebKit without intercepting game input")) return 1;
  NSDictionary *surfaces = [chrome valueForKey:@"surfaces"];
  if (!check(![[surfaces[@"hand"] valueForKey:@"selected"] boolValue] &&
             ![[surfaces[@"foot"] valueForKey:@"selected"] boolValue], "editing starts without selected outlines")) return 1;
  api.selected("hand", 1);
  if (!check([[surfaces[@"hand"] valueForKey:@"selected"] boolValue] &&
             ![[surfaces[@"foot"] valueForKey:@"selected"] boolValue], "only selected native target owns an outline")) return 1;
  api.exclusion(0.25, 0.8, 0.5, 0.15);
  CAShapeLayer *mask = (CAShapeLayer *)chrome.superview.layer.mask;
  if (!check([mask isKindOfClass:CAShapeLayer.class] && [mask.fillRule isEqualToString:kCAFillRuleEvenOdd] &&
             !CGPathContainsPoint(mask.path, nullptr, CGPointMake(320, 50), true) &&
             CGPathContainsPoint(mask.path, nullptr, CGPointMake(100, 50), true) &&
             chrome.superview == views[0].superview,
             "toolbar exclusion masks both WebKit and native chrome, preserving surrounding content")) return 1;
  api.exclusion(0, 0, 0.2, 0.2);
  if (!check(!CGPathContainsPoint(mask.path, nullptr, CGPointMake(30, 380), true) &&
             CGPathContainsPoint(mask.path, nullptr, CGPointMake(320, 50), true),
             "dragged toolbar exclusion moves immediately without retaining the old hole")) return 1;
  api.exclusion(0.25, 0.8, 0.5, 0.15);
  [game setContentSize:NSMakeSize(1280, 720)];
  Pump(0.1);
  mask = (CAShapeLayer *)chrome.superview.layer.mask;
  if (!check(!CGPathContainsPoint(mask.path, nullptr, CGPointMake(640, 90), true) &&
             CGPathContainsPoint(mask.path, nullptr, CGPointMake(200, 90), true),
             "normalized toolbar exclusion follows window resizing")) return 1;
  if (!check(api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             std::abs(x + width / 2 - 0.5) < 0.0001 && std::abs(y + height / 2 - 0.4) < 0.0001 &&
             std::abs(scale - 0.6) < 0.0001,
             "custom center and scale survive a window resize")) return 1;
  if (!check(api.surface("hand", source.UTF8String, 860, 540) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 && std::abs(scale - 0.6) < 0.0001,
             "descriptor refresh preserves customized layout")) return 1;
  Pump(0.1);
  if (!check(server.requests.load() == requests, "layout edits never reload the page")) return 1;
  if (!check(api.layout("hand", 1, 0, 0, 10) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             x + width >= width * .1 - .00001 && y + height >= height * .1 - .00001 &&
             x <= 1 - width * .1 + .00001 && y <= 1 - height * .1 + .00001 &&
             std::abs((width * 1280) / (height * 720) - 860.0 / 540) < 0.0001,
             "oversized layout retains 10 percent visibility without changing aspect ratio")) return 1;
  api.layout("hand", 1, -4, 5, 1);
  api.bounds("hand", &x, &y, &width, &height, &scale);
  if (!check(std::abs(x + width * .9) < .00001 && std::abs(y - (1 - height * .1)) < .00001,
             "native movement allows exactly 90 percent offscreen")) return 1;
  if (!check(api.layout("hand", 1, NAN, 0.5, 1) == 0 && api.layout("hand", 1, 0.5, 0.5, -1) == 0,
             "reject non-finite geometry and negative scale")) return 1;
  if (!check(api.layout("hand", 0, 0.5, 0.5, 1) == 1 && Nearly(views[0].frame.origin.x, 16) &&
             Nearly(views[0].frame.origin.y, 16), "restore automatic placement")) return 1;
  CGFloat originalWidth = views[0].frame.size.width;
  CGFloat originalHeight = views[0].frame.size.height;
  __block bool paddingPublished = false;
  [views[0] evaluateJavaScript:@"window.fixturePadding({gridSettings:{overlayPadding:30}}); true"
            completionHandler:^(id result, NSError *error) { paddingPublished = error == nil && [result boolValue]; }];
  Pump(0.3);
  api.bounds("hand", &x, &y, &width, &height, &scale);
  CGFloat inset = 30 * views[0].pageZoom;
  if (!check(paddingPublished && Nearly(width * 1280, originalWidth - inset * 2) &&
             Nearly(height * 720, originalHeight - inset * 2) && Nearly(x * 1280, 16) &&
             views[0].layer.mask != nil && Nearly(views[0].frame.size.width, originalWidth) &&
             Nearly(views[0].frame.size.height, originalHeight),
             "content padding is cropped from visual and hit bounds while preserving the CSS viewport and note area")) return 1;
  [views[0] evaluateJavaScript:@"window.fixturePadding({gridSettings:{overlayPadding:0}})" completionHandler:nil];
  Pump(0.2);
  if (!check(views[0].layer.mask == nil && Nearly(views[0].frame.origin.x, 16),
             "padding changes update without a viewer reload")) return 1;

  // Anchors and visibility policy are resolved by managed shared geometry. The
  // native ABI receives only the final normalized content rectangle, and must
  // apply it identically for game pixels, Retina points, and scale-only updates.
  double referenceWidth, referenceHeight;
  if (!check(api.referenceSize("hand", &referenceWidth, &referenceHeight) == 1 &&
             Nearly(referenceWidth, 430) && Nearly(referenceHeight, 270),
             "reference content size is independent of game window and backing scale")) return 1;
  // The managed layout sizes viewers like the separate window: natural CSS
  // pixels per desktop unit, measured against the game view in points.
  double contentWidth, contentHeight, viewWidth, viewHeight;
  // The 860x540 descriptor was fitted by .5 into the 430x270 reference.
  if (!check(api.contentSize("hand", &contentWidth, &contentHeight) == 1 && Nearly(contentWidth, 860) &&
             Nearly(contentHeight, 540) && api.viewSize(&viewWidth, &viewHeight) == 1 &&
             Nearly(viewWidth, game.contentView.bounds.size.width) &&
             Nearly(viewHeight, game.contentView.bounds.size.height),
             "natural content size and the game view are reported in desktop points")) return 1;
  auto applyBottomRight = [&](double gameWidth, double gameHeight, double uniformScale, double userScale) {
    double w = referenceWidth * uniformScale * userScale;
    double h = referenceHeight * uniformScale * userScale;
    return api.setBounds("hand", (gameWidth - w - 16 * uniformScale) / gameWidth,
        (gameHeight - h - 16 * uniformScale) / gameHeight, w / gameWidth, h / gameHeight, userScale);
  };
  unsigned long long revision = api.geometryRevision();
  if (!check(applyBottomRight(1280, 720, 2.0 / 3, 1) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             Nearly(width * 1280, 430 * 2.0 / 3) && Nearly(height * 720, 180) &&
             Nearly((1 - x - width) * 1280, 16 * 2.0 / 3) &&
             Nearly((1 - y - height) * 720, 16 * 2.0 / 3) && Nearly(scale, 1),
             "managed bottom-right rectangle preserves reference-unit margins and size")) return 1;
  NSRect pointFrame = views[0].frame;
  if (!check(applyBottomRight(2560, 1440, 4.0 / 3, 1) == 1 &&
             NSEqualRects(views[0].frame, pointFrame) && api.geometryRevision() == revision,
             "Retina game pixels map to the same AppKit frame without geometry revision feedback")) return 1;
  if (!check(applyBottomRight(1280, 720, 1, 1) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             Nearly(width * 1280, 430) && Nearly(views[0].pageZoom, .5),
             "uniform-scale-only updates change native size without resizing the host")) return 1;
  if (!check(api.setBounds("hand", -.3, .4, .4, .5, 7.5) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             std::abs(x + .3) < .00001 && std::abs(scale - 7.5) < .00001,
             "resolved bounds and migrated scales are not clamped a second time")) return 1;
  if (!check(api.setBounds("hand", NAN, 0, .3, .3, 1) == 0 &&
             api.setBounds("hand", 0, 0, 0, .3, 1) == 0,
             "resolved bounds reject invalid coordinates and zero size")) return 1;
  revision = api.geometryRevision();
  [game setContentSize:NSMakeSize(1600, 900)];
  Pump(.1);
  if (!check(api.geometryRevision() > revision &&
             api.referenceSize("hand", &referenceWidth, &referenceHeight) == 1 &&
             Nearly(referenceWidth, 430) && Nearly(referenceHeight, 270),
             "host resizing invalidates geometry without altering reference size")) return 1;
  [game setContentSize:NSMakeSize(1280, 720)];
  Pump(.1);
  revision = api.geometryRevision();
  [NSNotificationCenter.defaultCenter postNotificationName:NSWindowDidChangeBackingPropertiesNotification object:game];
  if (!check(api.geometryRevision() > revision,
             "backing changes invalidate native geometry even with unchanged host size")) return 1;
  revision = api.geometryRevision();
  [views[0] evaluateJavaScript:@"window.fixturePadding({gridSettings:{overlayPadding:30}})" completionHandler:nil];
  Pump(.2);
  if (!check(api.geometryRevision() > revision &&
             api.referenceSize("hand", &referenceWidth, &referenceHeight) == 1 &&
             Nearly(referenceWidth, 400) && Nearly(referenceHeight, 240) &&
             applyBottomRight(1280, 720, 2.0 / 3, 1) == 1 &&
             api.bounds("hand", &x, &y, &width, &height, &scale) == 1 &&
             Nearly(width * 1280, 400 * 2.0 / 3) && Nearly(height * 720, 160),
             "async CSS padding invalidates the cropped reference size for shared re-layout")) return 1;
  viewportReady = false;
  __block double resolvedInnerWidth = 0;
  [views[0] evaluateJavaScript:@"window.innerWidth" completionHandler:^(id result, NSError *error) {
    resolvedInnerWidth = [result doubleValue];
    // WebKit floors its layout viewport to whole AppKit points before undoing
    // pageZoom: 286.666pt at 1/3 zoom reports 858 CSS px (floor(286.666)/zoom).
    // Bound the error in rendered points instead of assuming an integer CSS
    // viewport at fractional zoom. The exact content frame stays unchanged.
    viewportReady = error == nil && std::abs(resolvedInnerWidth - 860) * views[0].pageZoom < 1.001;
  }];
  Pump(.2);
  if (!viewportReady || server.requests.load() != requests)
    fprintf(stderr, "Resolved viewport: innerWidth=%.9f frameWidth=%.9f zoom=%.9f requests=%d expected=%d\n",
      resolvedInnerWidth, views[0].frame.size.width, views[0].pageZoom, server.requests.load(), requests);
  if (!check(viewportReady && server.requests.load() == requests,
             "resolved geometry preserves CSS viewport within WebKit point rounding without page reload or IPC")) return 1;
  [views[0] evaluateJavaScript:@"window.fixturePadding({gridSettings:{overlayPadding:0}})" completionHandler:nil];
  Pump(.2);
  api.layout("hand", 0, .5, .5, 1);
  api.visible(0);
  if (!check(chrome.hidden, "settings visibility hides native edit chrome too")) return 1;
  api.visible(1);
  api.editing(0);
  if (!check(chrome.hidden && chrome.superview.layer.mask == nil,
             "leaving editing removes native chrome and toolbar exclusion")) return 1;

  // The desktop descriptor may still describe the two-key tab. The OBS page's
  // computed layout must drive both growth and shrinkage without navigation.
  if (!check(api.surface("foot", source.UTF8String, 133, 395) == 1 &&
             api.layout("foot", 1, .75, .6, .7) == 1,
             "bootstrap a foot viewer from stale desktop dimensions")) return 1;
  auto publishLayout = [&](double w, double h, double p) {
    NSString *script = [NSString stringWithFormat:
        @"(()=>{const r=document.getElementById('root');"
         "r.setAttribute('data-dmn-viewer-width','%.3f');"
         "r.setAttribute('data-dmn-viewer-height','%.3f');"
         "r.setAttribute('data-dmn-viewer-padding','%.3f');})()", w, h, p];
    [views[1] evaluateJavaScript:script completionHandler:nil];
    Pump(.2);
  };
  CGFloat initialZoom = views[1].pageZoom;
  CGFloat footReferenceScale = 430.0 / 640; // Fixed by the initial 640x360 descriptor.
  for (double cssWidth : {133.0, 209.0, 361.0, 2048.0, 133.0}) {
    double cssHeight = cssWidth == 2048 ? 1200 : 395;
    revision = api.geometryRevision();
    publishLayout(cssWidth, cssHeight, 30);
    CGFloat fit = footReferenceScale;
    if (!check(api.geometryRevision() > revision &&
               api.referenceSize("foot", &referenceWidth, &referenceHeight) == 1 &&
               Nearly(referenceWidth, (cssWidth - 60) * fit) && Nearly(referenceHeight, (cssHeight - 60) * fit) &&
               api.bounds("foot", &x, &y, &width, &height, &scale) == 1 &&
               Nearly(width * 1280, referenceWidth * .7) && Nearly(height * 720, referenceHeight * .7) &&
               std::abs(views[1].pageZoom - initialZoom) < .00001 &&
               std::abs(x + width / 2 - .75) < .00001 && std::abs(y + height / 2 - .6) < .00001 &&
               std::abs(scale - .7) < .00001 &&
               api.contentSize("foot", &contentWidth, &contentHeight) == 1 &&
               Nearly(contentWidth, cssWidth - 60) && Nearly(contentHeight, cssHeight - 60),
               "content growth beyond 430x270 preserves CSS pixel size and user scale")) return 1;
    __block double cssViewport = 0;
    [views[1] evaluateJavaScript:@"window.innerWidth" completionHandler:^(id result, NSError *error) {
      if (error == nil) cssViewport = [result doubleValue];
    }];
    Pump(.1);
    revision = api.geometryRevision();
    NSRect liveFrame = views[1].frame;
    if (!check(std::abs(cssViewport - cssWidth) * views[1].pageZoom < 1.001 &&
               api.surface("foot", source.UTF8String, 133, 395) == 1 &&
               api.geometryRevision() == revision && NSEqualRects(views[1].frame, liveFrame) &&
               server.requests.load() == requests,
               "descriptor polling cannot overwrite the live CSS viewport or reload the page")) return 1;
  }
  revision = api.geometryRevision();
  publishLayout(133, 395, 30);
  [views[1] evaluateJavaScript:@"document.getElementById('fixture').setAttribute('data-state','active')"
            completionHandler:nil];
  Pump(.1);
  if (!check(api.geometryRevision() == revision,
             "unchanged dimensions and key highlights do not invalidate native layout")) return 1;

  // Model the five-point managed resolver with actual native views. Keep each
  // chosen point fixed while both content dimensions cross the old fit limits.
  const double anchorCases[][4] = {
      {0, 0, 80, 50}, {1, 0, 1200, 50}, {0, 1, 80, 670},
      {1, 1, 1200, 670}, {.5, .5, 640, 360}};
  for (const auto &anchor : anchorCases) {
    for (const auto &size : {NSMakeSize(133, 395), NSMakeSize(2048, 1200), NSMakeSize(133, 395)}) {
      publishLayout(size.width, size.height, 30);
      api.referenceSize("foot", &referenceWidth, &referenceHeight);
      double w = referenceWidth * .7, h = referenceHeight * .7;
      double left = anchor[2] - anchor[0] * w, top = anchor[3] - anchor[1] * h;
      if (!check(api.setBounds("foot", left / 1280, top / 720, w / 1280, h / 720, .7) == 1 &&
                 api.bounds("foot", &x, &y, &width, &height, &scale) == 1 &&
                 std::abs((x + anchor[0] * width) * 1280 - anchor[2]) < .00001 &&
                 std::abs((y + anchor[1] * height) * 720 - anchor[3]) < .00001 &&
                 std::abs(views[1].pageZoom - initialZoom) < .00001 &&
                 server.requests.load() == requests,
                 "four corners and center remain fixed through large content growth and shrinkage")) return 1;
    }
  }
  revision = api.geometryRevision();
  publishLayout(NAN, 395, 30);
  publishLayout(5000, 395, 30);
  if (!check(api.geometryRevision() == revision,
             "invalid page dimensions do not replace the last valid viewport")) return 1;
  publishLayout(1, 1, 0);
  if (!check(api.referenceSize("foot", &referenceWidth, &referenceHeight) == 1 &&
             std::abs(referenceWidth - footReferenceScale) < .00001 &&
             std::abs(referenceHeight - footReferenceScale) < .00001,
             "empty tabs clear the previous keyviewer footprint")) return 1;
  NSString *newSource = [source stringByAppendingString:@"&layout=reload"];
  if (!check(api.surface("foot", newSource.UTF8String, 860, 540) == 1,
             "navigate the foot viewer to a replacement source")) return 1;
  Pump(.3);
  if (!check(api.referenceSize("foot", &referenceWidth, &referenceHeight) == 1 &&
             Nearly(referenceWidth, 860 * footReferenceScale) && Nearly(referenceHeight, 540 * footReferenceScale),
             "a new page resets reported dimensions to its descriptor until it publishes layout")) return 1;

  [views[0] loadRequest:[NSURLRequest requestWithURL:[NSURL URLWithString:@"https://example.com/?token=must-not-leak"]]];
  Pump(0.2);
  if (!check(strstr(api.error(), "blocked navigation") != nullptr &&
             strstr(api.error(), "must-not-leak") == nullptr,
             "navigation delegate rejects an external origin without token disclosure")) return 1;
  if (!check(api.surface("foot", nullptr, 0, 0) == 1 && WebViews(game).size() == 1,
             "remove one viewer independently")) return 1;
  if (!check(api.bounds("foot", &x, &y, &width, &height, &scale) == 0 &&
             api.selectionBounds("foot", &x, &y, &width, &height, &scale) == 0,
             "deleted viewers retain no editing or selection hit target")) return 1;
  if (!check(api.surface("hand", nullptr, 0, 0) == 1 && WebViews(game).empty(),
             "deleting both viewers removes both WebKit processes from host")) return 1;
  if (!check(api.surface("hand:first", source.UTF8String, 860, 540) == 1 &&
             api.surface("hand:second", source.UTF8String, 860, 540) == 1 && WebViews(game).size() == 2,
             "multiple instances can share one hand viewer source")) return 1;
  api.layout("hand:first", 1, .2, .3, .5);
  api.layout("hand:second", 1, .7, .6, 1.2);
  if (!check(api.bounds("hand:first", &x, &y, &width, &height, &scale) == 1 &&
             std::abs(x + width/2 - .2) < .0001 && std::abs(scale - .5) < .0001 &&
             api.bounds("hand:second", &x, &y, &width, &height, &scale) == 1 &&
             std::abs(x + width/2 - .7) < .0001 && std::abs(scale - 1.2) < .0001,
             "same-kind instances have independent position and size")) return 1;
  if (!check(api.surface("hand:first", nullptr, 0, 0) == 1 && WebViews(game).size() == 1 &&
             api.bounds("hand:second", &x, &y, &width, &height, &scale) == 1,
             "deleting a duplicate leaves the other instance alive")) return 1;
  api.dispose();
  Pump(0.1);
  if (!check(WebViews(game).empty() && game.firstResponder == originalResponder,
             "dispose detaches native views and preserves focus")) return 1;
  if (!check(api.surface("hand", source.UTF8String, 860, 540) == 1,
             "reattach after mod-style dispose")) return 1;
  api.dispose();
  [game orderOut:nil];
  printf("PASS: native C ABI, window selection, local HTML, CSS viewport, transparency, "
         "input pass-through, reuse, resize, layout, editing chrome, visibility, navigation, removal, reload.\n");
  printf("Discord/OBS capture and the real Unity render hierarchy require manual game verification.\n");
  return 0;
}
} // namespace

int main(int argc, char **argv) {
  @autoreleasepool {
    if (argc != 2) {
      fprintf(stderr, "Usage: GameWebViewSmoke /absolute/path/libElementGameWebView.dylib\n");
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
