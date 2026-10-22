#import "GameWebView.h"
#import <AppKit/AppKit.h>
#import <WebKit/WebKit.h>

#include <algorithm>
#include <cmath>
#include <cstring>
#include <exception>
#include <mutex>
#include <string>

namespace {
constexpr CGFloat kInset = 16;
constexpr CGFloat kMaximumWidth = 430;
constexpr CGFloat kMaximumHeight = 270;
constexpr int kMaximumViewport = 4096;
std::mutex errorMutex;
std::string lastError;

void SetError(const char *message) {
  std::lock_guard<std::mutex> lock(errorMutex);
  lastError = message;
}

// Deliberately do not include exception descriptions or NSError URLs: the OBS
// viewer authenticates through a query token, which must not enter game logs.
int OnMainThread(int (^work)(void)) {
  __block int result = 0;
  void (^guarded)(void) = ^{
    @try {
      try {
        result = work();
      } catch (const std::exception &) {
        SetError("Native viewer operation failed.");
      } catch (...) {
        SetError("Native viewer operation failed.");
      }
    } @catch (NSException *) {
      SetError("macOS rejected the native viewer operation.");
    }
  };
  if ([NSThread isMainThread]) {
    guarded();
  } else {
    dispatch_sync(dispatch_get_main_queue(), guarded);
  }
  return result;
}

bool IsLoopbackURL(NSURL *url) {
  NSString *host = url.host.lowercaseString;
  return [url.scheme.lowercaseString isEqualToString:@"http"] &&
         ([(host ?: @"") isEqualToString:@"127.0.0.1"] ||
          [host isEqualToString:@"localhost"] ||
          [host isEqualToString:@"::1"] ||
          [host isEqualToString:@"[::1]"]) &&
         url.port != nil && url.port.integerValue > 0 &&
         url.port.integerValue <= 65535 && url.user == nil &&
         url.password == nil;
}

bool IsSameOrigin(NSURL *left, NSURL *right) {
  return IsLoopbackURL(left) && IsLoopbackURL(right) &&
         [left.host.lowercaseString isEqualToString:right.host.lowercaseString] &&
         [left.port isEqualToNumber:right.port];
}

int UnityViewScore(NSView *view) {
  NSString *name = NSStringFromClass(view.class);
  int score = 0;
  // ADOFAI's installed UnityPlayer uses PlayerWindow/PlayerWindowView. Other
  // Unity player versions use Unity* names; do not fall back to arbitrary views.
  if ([name isEqualToString:@"PlayerWindowView"] ||
      [name isEqualToString:@"UnityView"] ||
      [name isEqualToString:@"UnityRenderingView"] ||
      [name isEqualToString:@"UnityMetalView"] ||
      [name isEqualToString:@"UnityOpenGLView"]) {
    score = 100;
  } else if ([name hasPrefix:@"Unity"]) {
    score = 50;
  }
  for (NSView *child in view.subviews) {
    score = std::max(score, UnityViewScore(child));
  }
  return score;
}

NSWindow *FindGameWindow(NSWindow *previous) {
  if (previous != nil && [NSApp.windows containsObject:previous] &&
      UnityViewScore(previous.contentView) > 0) {
    return previous;
  }
  NSWindow *best = nil;
  int bestScore = 0;
  for (NSWindow *window in NSApp.windows) {
    if ([window isKindOfClass:NSPanel.class] || !window.visible ||
        window.contentView == nil || window.parentWindow != nil) {
      continue;
    }
    int score = UnityViewScore(window.contentView);
    if (score == 0) continue;
    NSString *windowClass = NSStringFromClass(window.class);
    if ([windowClass isEqualToString:@"PlayerWindow"] ||
        [windowClass hasPrefix:@"Unity"]) score += 20;
    if (window == NSApp.mainWindow) score += 5;
    if (window.contentView.bounds.size.width >= 640 &&
        window.contentView.bounds.size.height >= 360) {
      score += 10;
    }
    if (score > bestScore) {
      best = window;
      bestScore = score;
    }
  }
  return best;
}
} // namespace

@interface IRPGameWebView : WKWebView
@end

@implementation IRPGameWebView
- (BOOL)acceptsFirstResponder { return NO; }
- (BOOL)becomeFirstResponder { return NO; }
- (NSView *)hitTest:(NSPoint)point { return nil; }
@end

@interface IRPGameWebViewSurface : NSObject <WKNavigationDelegate, WKUIDelegate>
@property(nonatomic, strong) IRPGameWebView *webView;
@property(nonatomic, strong) NSURL *url;
@property(nonatomic) NSSize viewport;
@end

@implementation IRPGameWebViewSurface
- (void)webView:(WKWebView *)webView
    decidePolicyForNavigationAction:(WKNavigationAction *)action
                   decisionHandler:(void (^)(WKNavigationActionPolicy))handler {
  NSURL *target = action.request.URL;
  if (IsSameOrigin(self.url, target) && action.targetFrame != nil) {
    handler(WKNavigationActionPolicyAllow);
  } else {
    SetError("Native viewer blocked navigation outside its local source.");
    handler(WKNavigationActionPolicyCancel);
  }
}

- (WKWebView *)webView:(WKWebView *)webView
    createWebViewWithConfiguration:(WKWebViewConfiguration *)configuration
              forNavigationAction:(WKNavigationAction *)action
                   windowFeatures:(WKWindowFeatures *)features {
  return nil;
}

- (void)webView:(WKWebView *)webView
    didFailProvisionalNavigation:(WKNavigation *)navigation
                      withError:(NSError *)error {
  if (error.code != NSURLErrorCancelled) {
    self.url = nil;
    SetError("Native viewer could not load its local source. Check ImplDmNote.");
  }
}

- (void)webView:(WKWebView *)webView
    didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
  if (error.code != NSURLErrorCancelled) {
    self.url = nil;
    SetError("Native viewer lost its local source. Check ImplDmNote.");
  }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
  SetError("");
}

- (void)webViewWebContentProcessDidTerminate:(WKWebView *)webView {
  // The descriptor poll, rather than an immediate reload loop in the renderer
  // callback, can retry after the process has terminated.
  self.url = nil;
  SetError("Native viewer renderer stopped. Re-enable the native preview.");
}
@end

@interface IRPGameWebViewHost : NSView
@property(nonatomic, strong)
    NSMutableDictionary<NSString *, IRPGameWebViewSurface *> *surfaces;
@property(nonatomic) BOOL previewVisible;
- (void)layoutSurfaces;
@end

@implementation IRPGameWebViewHost
- (instancetype)initWithFrame:(NSRect)frame {
  self = [super initWithFrame:frame];
  if (self) {
    self.surfaces = [NSMutableDictionary dictionary];
    self.previewVisible = YES;
    self.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
  }
  return self;
}
- (BOOL)isFlipped { return NO; }
- (BOOL)acceptsFirstResponder { return NO; }
- (NSView *)hitTest:(NSPoint)point { return nil; }
- (void)setFrameSize:(NSSize)newSize {
  [super setFrameSize:newSize];
  [self layoutSurfaces];
}
- (void)layoutSurfaces {
  CGFloat availableWidth = std::max<CGFloat>(0, (self.bounds.size.width - 3 * kInset) / 2);
  CGFloat availableHeight = std::max<CGFloat>(0, self.bounds.size.height - 2 * kInset);
  for (NSString *kind in self.surfaces) {
    IRPGameWebViewSurface *surface = self.surfaces[kind];
    NSSize source = surface.viewport;
    if (source.width <= 0 || source.height <= 0) continue;
    CGFloat scale = std::min<CGFloat>(
        1, std::min(std::min(kMaximumWidth, availableWidth) / source.width,
                    std::min(kMaximumHeight, availableHeight) / source.height));
    BOOL canDisplay = self.previewVisible && scale > 0;
    surface.webView.hidden = !canDisplay;
    if (!canDisplay) continue;
    NSSize size = NSMakeSize(source.width * scale, source.height * scale);
    CGFloat x = [kind isEqualToString:@"hand"]
                    ? kInset
                    : self.bounds.size.width - kInset - size.width;
    surface.webView.frame = NSMakeRect(x, kInset, size.width, size.height);
    // Preserve the CSS viewport used by the desktop/OBS renderer while fitting
    // the visual surface into game points (independent of Retina backing scale).
    surface.webView.pageZoom = scale;
  }
}
@end

namespace {
IRPGameWebViewHost *host;
__weak NSWindow *gameWindow;
NSMutableArray<id> *windowObservers;
bool previewVisible = true;

bool EnsureHost() {
  if (NSApp == nil) {
    SetError("The macOS application is not ready for a native viewer.");
    return false;
  }
  NSWindow *window = FindGameWindow(gameWindow);
  if (window == nil) {
    SetError("The Unity game window was not found. Return to the game and retry.");
    return false;
  }
  if (host == nil) host = [[IRPGameWebViewHost alloc] initWithFrame:window.contentView.bounds];
  gameWindow = window;
  host.previewVisible = previewVisible;
  if (host.superview != window.contentView) {
    [host removeFromSuperview];
    host.frame = window.contentView.bounds;
    [window.contentView addSubview:host positioned:NSWindowAbove relativeTo:nil];
  }
  [host layoutSurfaces];
  if (windowObservers == nil) {
    windowObservers = [NSMutableArray array];
    for (NSNotificationName name in @[
           NSWindowDidEnterFullScreenNotification,
           NSWindowDidExitFullScreenNotification,
           NSWindowDidResizeNotification,
           NSWindowDidDeminiaturizeNotification
         ]) {
      id observer = [NSNotificationCenter.defaultCenter
          addObserverForName:name object:nil queue:NSOperationQueue.mainQueue
                  usingBlock:^(NSNotification *notification) {
                    if (notification.object == gameWindow) {
                      OnMainThread(^{ return EnsureHost() ? 1 : 0; });
                    }
                  }];
      [windowObservers addObject:observer];
    }
  }
  return true;
}

void RemoveSurface(NSString *kind) {
  IRPGameWebViewSurface *surface = host.surfaces[kind];
  if (surface == nil) return;
  surface.webView.navigationDelegate = nil;
  surface.webView.UIDelegate = nil;
  [surface.webView stopLoading];
  [surface.webView removeFromSuperview];
  [host.surfaces removeObjectForKey:kind];
}
} // namespace

extern "C" __attribute__((visibility("default"))) int irp_webview_supported(void) {
  if (@available(macOS 11.0, *)) return 1;
  SetError("Native viewer requires macOS 11 or later.");
  return 0;
}

extern "C" __attribute__((visibility("default"))) int irp_webview_set_surface(
    const char *viewerKind, const char *url, int width, int height) {
  if (!irp_webview_supported()) return 0;
  if (viewerKind == nullptr ||
      (strcmp(viewerKind, "hand") != 0 && strcmp(viewerKind, "foot") != 0)) {
    SetError("Native viewer kind must be hand or foot.");
    return 0;
  }
  bool remove = url == nullptr || url[0] == '\0';
  if (!remove && (width < 1 || height < 1 || width > kMaximumViewport ||
                  height > kMaximumViewport)) {
    SetError("Native viewer requires a local HTTP URL and a viewport from 1 to 4096.");
    return 0;
  }
  return OnMainThread(^{
    NSString *kind = [NSString stringWithUTF8String:viewerKind];
    if (remove) {
      RemoveSurface(kind);
      SetError("");
      return 1;
    }
    NSString *sourceString = [NSString stringWithUTF8String:url];
    NSURL *source = sourceString == nil ? nil : [NSURL URLWithString:sourceString];
    if (!IsLoopbackURL(source)) {
      SetError("Native viewer requires a local HTTP URL and a viewport from 1 to 4096.");
      return 0;
    }
    if (!EnsureHost()) return 0;
    IRPGameWebViewSurface *surface = host.surfaces[kind];
    if (surface == nil) {
      surface = [[IRPGameWebViewSurface alloc] init];
      WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
      configuration.websiteDataStore = WKWebsiteDataStore.nonPersistentDataStore;
      configuration.preferences.javaScriptCanOpenWindowsAutomatically = NO;
      surface.webView = [[IRPGameWebView alloc] initWithFrame:NSZeroRect configuration:configuration];
      surface.webView.navigationDelegate = surface;
      surface.webView.UIDelegate = surface;
      surface.webView.allowsBackForwardNavigationGestures = NO;
      surface.webView.allowsLinkPreview = NO;
      // WKWebView exposes transparent drawing via this WebKit property on macOS.
      // The outer exception boundary reports incompatibility without crashing Unity.
      [surface.webView setValue:@NO forKey:@"drawsBackground"];
      [host addSubview:surface.webView];
      host.surfaces[kind] = surface;
    }
    surface.viewport = NSMakeSize(width, height);
    [host layoutSurfaces];
    if (![surface.url isEqual:source]) {
      surface.url = source;
      SetError("");
      [surface.webView loadRequest:[NSURLRequest requestWithURL:source
                                                  cachePolicy:NSURLRequestReloadIgnoringLocalCacheData
                                              timeoutInterval:10]];
    }
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_visible(int visible) {
  OnMainThread(^{
    previewVisible = visible != 0;
    host.previewVisible = previewVisible;
    [host layoutSurfaces];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_dispose(void) {
  OnMainThread(^{
    for (id observer in windowObservers) {
      [NSNotificationCenter.defaultCenter removeObserver:observer];
    }
    windowObservers = nil;
    RemoveSurface(@"hand");
    RemoveSurface(@"foot");
    [host removeFromSuperview];
    host = nil;
    gameWindow = nil;
    previewVisible = true;
    SetError("");
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) const char *irp_webview_last_error(void) {
  thread_local std::string result;
  std::lock_guard<std::mutex> lock(errorMutex);
  result = lastError;
  return result.c_str();
}
