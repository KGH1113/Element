#import "GameWebView.h"
#import "ViewerScripts.h"
#import <AppKit/AppKit.h>
#import <QuartzCore/QuartzCore.h>
#import <WebKit/WebKit.h>

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstring>
#include <exception>
#include <map>
#include <mutex>
#include <string>
#include <vector>

namespace {
constexpr CGFloat kInset = 16;
constexpr CGFloat kMaximumWidth = 430;
constexpr CGFloat kMaximumHeight = 270;
constexpr int kMaximumViewport = 4096;
constexpr CGFloat kMinimumVisibleFraction = 0.1;
// Host errors describe the latest call. Faults belong to one viewer, so one
// page loading never clears another's.
std::mutex errorMutex;
std::string hostError;
std::map<std::string, std::string> faults;
std::atomic<unsigned long long> geometryRevision{1};
// The size Unity draws the game at, in its pixels; zero until the game says.
NSSize gameSize = NSZeroSize;
// What overlay elements in front of each viewer cover, by viewer identifier:
// groups of x, y, width and height normalized to the game, top-left origin.
// Kept apart from the pages, so covers sent before a page attaches or across
// its replacement still apply. Main thread only.
constexpr int kMaximumCovers = 4096;
std::map<std::string, std::vector<double>> covers;

bool ValidKind(const char *kind) {
  return kind != nullptr && strlen(kind) <= 160 &&
         (strcmp(kind, "hand") == 0 || strcmp(kind, "foot") == 0 || strncmp(kind, "hand:", 5) == 0 ||
          strncmp(kind, "foot:", 5) == 0);
}

// Where the game is drawn in its window. A game drawn at another aspect than
// the window (fullscreen at a lower resolution) is fitted and centered, with
// bars beside it. The host covers this rectangle, so pages, editor cutouts
// and pointer readings share the game's own coordinates.
NSRect GameRect(NSRect bounds) {
  if (gameSize.width <= 0 || gameSize.height <= 0 || bounds.size.width <= 0 || bounds.size.height <= 0)
    return bounds;
  CGFloat scale = std::min(bounds.size.width / gameSize.width, bounds.size.height / gameSize.height);
  NSSize fitted = NSMakeSize(gameSize.width * scale, gameSize.height * scale);
  // Within a point of the window, the game fills it: windowed, or at the window's own resolution.
  if (bounds.size.width - fitted.width < 1 && bounds.size.height - fitted.height < 1) return bounds;
  return NSMakeRect(NSMinX(bounds) + (bounds.size.width - fitted.width) / 2,
                    NSMinY(bounds) + (bounds.size.height - fitted.height) / 2, fitted.width, fitted.height);
}

void SetError(const char *message) {
  std::lock_guard<std::mutex> lock(errorMutex);
  hostError = message;
}

void SetFault(NSString *kind, const char *message) {
  if (kind == nil) return;
  std::lock_guard<std::mutex> lock(errorMutex);
  std::string key = kind.UTF8String;
  if (message == nullptr || message[0] == '\0') faults.erase(key);
  else faults[key] = message;
}

void ClearFaults() {
  std::lock_guard<std::mutex> lock(errorMutex);
  faults.clear();
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

// Holds one viewer's page at its web frame. Covers are cut out of it with a
// mask of its own, nested inside the host's editor mask, so the two may
// overlap and the viewer is hidden where either is. Unflipped like the host,
// so the mask uses the host's y-up points shifted to its origin.
@interface IRPGameWebViewClip : NSView
@end

@implementation IRPGameWebViewClip
- (BOOL)isFlipped { return NO; }
- (BOOL)acceptsFirstResponder { return NO; }
- (NSView *)hitTest:(NSPoint)point { return nil; }
@end

@interface IRPGameWebViewPaddingBridge : NSObject <WKScriptMessageHandler>
@property(nonatomic, copy) void (^changed)(CGFloat);
@end
@implementation IRPGameWebViewPaddingBridge
- (void)userContentController:(WKUserContentController *)controller didReceiveScriptMessage:(WKScriptMessage *)message {
  if (!message.frameInfo.isMainFrame || ![message.body isKindOfClass:NSNumber.class]) return;
  double value = [message.body doubleValue];
  if (std::isfinite(value) && value >= 0 && value <= kMaximumViewport && self.changed) self.changed(value);
}
@end

@interface IRPGameWebViewSelectionBridge : NSObject <WKScriptMessageHandler>
@property(nonatomic, copy) void (^changed)(NSRect);
@end
@implementation IRPGameWebViewSelectionBridge
- (void)userContentController:(WKUserContentController *)controller didReceiveScriptMessage:(WKScriptMessage *)message {
  if (!message.frameInfo.isMainFrame || ![message.body isKindOfClass:NSArray.class]) return;
  NSArray *values = message.body;
  if (values.count != 4) return;
  double parsed[4];
  for (NSUInteger i = 0; i < 4; i++) {
    if (![values[i] isKindOfClass:NSNumber.class]) return;
    parsed[i] = [values[i] doubleValue];
    if (!std::isfinite(parsed[i]) || parsed[i] < 0 || parsed[i] > 1) return;
  }
  if (parsed[0] + parsed[2] > 1.001 || parsed[1] + parsed[3] > 1.001) return;
  if (self.changed) self.changed(NSMakeRect(parsed[0], parsed[1], parsed[2], parsed[3]));
}
@end

@interface IRPGameWebViewLayoutBridge : NSObject <WKScriptMessageHandler>
@property(nonatomic, copy) void (^changed)(NSSize, CGFloat);
@end
@implementation IRPGameWebViewLayoutBridge
- (void)userContentController:(WKUserContentController *)controller didReceiveScriptMessage:(WKScriptMessage *)message {
  if (!message.frameInfo.isMainFrame || ![message.body isKindOfClass:NSArray.class]) return;
  NSArray *values = message.body;
  if (values.count != 3) return;
  double parsed[3];
  for (NSUInteger i = 0; i < 3; i++) {
    if (![values[i] isKindOfClass:NSNumber.class]) return;
    parsed[i] = [values[i] doubleValue];
    if (!std::isfinite(parsed[i]) || parsed[i] < 0 || parsed[i] > kMaximumViewport) return;
  }
  if (parsed[0] < 1 || parsed[1] < 1 || parsed[2] * 2 >= std::min(parsed[0], parsed[1])) return;
  if (self.changed) self.changed(NSMakeSize(parsed[0], parsed[1]), parsed[2]);
}
@end

// Page scripts are shared with the Windows host (ViewerScripts.h). This prelude
// routes their window.__irpPost(name, value) calls to WebKit message handlers.
static NSString *BridgeScript() {
  return @"(() => { if (window.__irpPost) return;"
    "const post = (name, value) => window.webkit.messageHandlers[name].postMessage(value);"
    "Object.defineProperty(window, '__irpPost', { value: post }); })();";
}

static WKUserScript *PageScript(NSString *source) {
  return [[WKUserScript alloc] initWithSource:source
                                injectionTime:WKUserScriptInjectionTimeAtDocumentEnd
                             forMainFrameOnly:YES];
}

@interface IRPGameWebViewSurface : NSObject <WKNavigationDelegate, WKUIDelegate>
@property(nonatomic, copy) NSString *kind;
@property(nonatomic, strong) IRPGameWebView *webView;
@property(nonatomic, strong) IRPGameWebViewClip *clip;
@property(nonatomic, strong) CAShapeLayer *coverMask;
@property(nonatomic, strong) NSURL *url;
@property(nonatomic) NSSize viewport;
@property(nonatomic) NSSize reportedViewport;
@property(nonatomic) CGFloat referenceScale;
@property(nonatomic) CGFloat padding;
@property(nonatomic) NSRect contentFrame;
@property(nonatomic) NSRect selectionInViewport;
@property(nonatomic, strong) CAShapeLayer *contentMask;
@property(nonatomic) BOOL customized;
@property(nonatomic) NSPoint normalizedCenter;
@property(nonatomic) CGFloat layoutScale;
@property(nonatomic) CGFloat effectiveScale;
@property(nonatomic) BOOL explicitBounds;
@property(nonatomic) NSRect normalizedBounds;
@property(nonatomic) BOOL selected;
@property(nonatomic) BOOL editing;
- (void)applyCoversInHost:(NSSize)size;
@end

@implementation IRPGameWebViewSurface {
  // The clip size and cover rectangles the mask was last built from.
  std::vector<double> _appliedCovers;
}

// Cuts this viewer's covers out of its clip. Each is kept to the content the
// viewer shows and moved from host points into the clip's own.
- (void)applyCoversInHost:(NSSize)size {
  IRPGameWebViewClip *clip = self.clip;
  if (clip == nil) return;
  NSRect origin = clip.frame;
  std::vector<double> applied;
  CGMutablePathRef path = nullptr;
  auto found = covers.find(self.kind.UTF8String);
  if (found != covers.end() && size.width > 0 && size.height > 0) {
    const std::vector<double> &values = found->second;
    for (size_t i = 0; i + 3 < values.size(); i += 4) {
      NSRect cover = NSIntersectionRect(self.contentFrame, NSMakeRect(
          values[i] * size.width, (1 - values[i + 1] - values[i + 3]) * size.height,
          values[i + 2] * size.width, values[i + 3] * size.height));
      if (NSIsEmptyRect(cover)) continue;
      cover = NSOffsetRect(cover, -NSMinX(origin), -NSMinY(origin));
      if (path == nullptr) {
        // Covers never overlap, so even-odd over the whole clip leaves exactly them out.
        path = CGPathCreateMutable();
        CGPathAddRect(path, nullptr, NSRectToCGRect(clip.bounds));
        applied.insert(applied.end(), {clip.bounds.size.width, clip.bounds.size.height});
      }
      CGPathAddRect(path, nullptr, NSRectToCGRect(cover));
      applied.insert(applied.end(), {cover.origin.x, cover.origin.y, cover.size.width, cover.size.height});
    }
  }
  if (path == nullptr) {
    if (clip.layer.mask != nil) {
      [CATransaction begin];
      [CATransaction setDisableActions:YES];
      clip.layer.mask = nil;
      [CATransaction commit];
    }
    _appliedCovers.clear();
    return;
  }
  if (clip.layer.mask == self.coverMask && applied == _appliedCovers) {
    CGPathRelease(path);
    return;
  }
  if (self.coverMask == nil) {
    self.coverMask = [CAShapeLayer layer];
    self.coverMask.fillRule = kCAFillRuleEvenOdd;
    self.coverMask.fillColor = NSColor.whiteColor.CGColor;
  }
  // Without implicit animations: a text's new glyphs are cut the frame they show.
  [CATransaction begin];
  [CATransaction setDisableActions:YES];
  self.coverMask.frame = clip.bounds;
  self.coverMask.path = path;
  clip.layer.mask = self.coverMask;
  [CATransaction commit];
  _appliedCovers = applied;
  CGPathRelease(path);
}

- (void)webView:(WKWebView *)webView
    decidePolicyForNavigationAction:(WKNavigationAction *)action
                   decisionHandler:(void (^)(WKNavigationActionPolicy))handler {
  NSURL *target = action.request.URL;
  if (IsSameOrigin(self.url, target) && action.targetFrame != nil) {
    handler(WKNavigationActionPolicyAllow);
  } else {
    SetFault(self.kind, "Native viewer blocked navigation outside its local source.");
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
    SetFault(self.kind, "Native viewer could not load its local source. Check ElementNote.");
  }
}

- (void)webView:(WKWebView *)webView
    didFailNavigation:(WKNavigation *)navigation withError:(NSError *)error {
  if (error.code != NSURLErrorCancelled) {
    self.url = nil;
    SetFault(self.kind, "Native viewer lost its local source. Check ElementNote.");
  }
}

- (void)webView:(WKWebView *)webView didFinishNavigation:(WKNavigation *)navigation {
  SetFault(self.kind, "");
  [webView evaluateJavaScript:self.editing ? @"window.__irpSetEditing?.(true);" : @"window.__irpSetEditing?.(false);"
           completionHandler:nil];
}

- (void)webViewWebContentProcessDidTerminate:(WKWebView *)webView {
  // The descriptor poll, rather than an immediate reload loop in the renderer
  // callback, can retry after the process has terminated.
  self.url = nil;
  SetFault(self.kind, "Native viewer renderer stopped. Viewers will reconnect automatically.");
}
@end

@interface IRPGameWebViewChrome : NSView
@property(nonatomic, strong)
    NSMutableDictionary<NSString *, IRPGameWebViewSurface *> *surfaces;
@end

@implementation IRPGameWebViewChrome
- (BOOL)acceptsFirstResponder { return NO; }
- (NSView *)hitTest:(NSPoint)point { return nil; }
- (void)drawRect:(NSRect)dirtyRect {
  CGFloat pixelsPerPoint = std::max<CGFloat>(1, self.window.backingScaleFactor);
  CGFloat unit = std::clamp<CGFloat>(self.bounds.size.height * pixelsPerPoint / 1000, 1, 2) / pixelsPerPoint;
  for (IRPGameWebViewSurface *surface in self.surfaces.allValues) {
    if (surface.webView.hidden || !surface.selected) continue;
    NSRect frame = surface.contentFrame;
    NSRect inset = NSInsetRect(frame, 1.5 * unit, 1.5 * unit);
    CGFloat radius = std::min<CGFloat>(8 * unit, std::min(inset.size.width, inset.size.height) / 2);
    NSBezierPath *outline = [NSBezierPath bezierPathWithRoundedRect:inset xRadius:radius yRadius:radius];
    CGFloat dash[] = {8 * unit, 5 * unit};
    [outline setLineDash:dash count:2 phase:0];
    outline.lineWidth = 3 * unit;
    [NSColor.whiteColor setStroke];
    [outline stroke];
    NSPoint handles[] = {
        NSMakePoint(NSMinX(frame), NSMinY(frame)), NSMakePoint(NSMidX(frame), NSMinY(frame)),
        NSMakePoint(NSMaxX(frame), NSMinY(frame)), NSMakePoint(NSMaxX(frame), NSMidY(frame)),
        NSMakePoint(NSMaxX(frame), NSMaxY(frame)), NSMakePoint(NSMidX(frame), NSMaxY(frame)),
        NSMakePoint(NSMinX(frame), NSMaxY(frame)), NSMakePoint(NSMinX(frame), NSMidY(frame))};
    for (NSPoint point : handles) {
      NSRect handle = NSMakeRect(point.x - 3.5 * unit, point.y - 3.5 * unit, 7 * unit, 7 * unit);
      NSBezierPath *path = [NSBezierPath bezierPathWithOvalInRect:handle];
      [NSColor.whiteColor setFill];
      [path fill];
      [[NSColor colorWithWhite:0 alpha:0.75] setStroke];
      path.lineWidth = unit;
      [path stroke];
    }
  }
}
@end

@interface IRPGameWebViewHost : NSView
@property(nonatomic, strong)
    NSMutableDictionary<NSString *, IRPGameWebViewSurface *> *surfaces;
@property(nonatomic) BOOL previewVisible;
@property(nonatomic) BOOL editing;
@property(nonatomic, strong) IRPGameWebViewChrome *chrome;
@property(nonatomic, strong) CAShapeLayer *editorMask;
- (void)layoutSurfaces;
- (void)updateEditorMask;
- (void)setEditorExclusions:(const double *)values count:(int)count;
@end

@implementation IRPGameWebViewHost {
  // Groups of x, y, width, height and radius; see irp_webview_set_editor_exclusions.
  std::vector<double> _editorExclusions;
  std::vector<double> _appliedMask;
}
- (instancetype)initWithFrame:(NSRect)frame {
  self = [super initWithFrame:frame];
  if (self) {
    self.surfaces = [NSMutableDictionary dictionary];
    self.previewVisible = YES;
    self.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
    self.wantsLayer = YES;
    self.layer.masksToBounds = YES;
    self.chrome = [[IRPGameWebViewChrome alloc] initWithFrame:self.bounds];
    self.chrome.surfaces = self.surfaces;
    self.chrome.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
    self.chrome.hidden = YES;
    [self addSubview:self.chrome];
  }
  return self;
}
- (BOOL)isFlipped { return NO; }
- (BOOL)acceptsFirstResponder { return NO; }
- (NSView *)hitTest:(NSPoint)point { return nil; }
- (void)setFrameSize:(NSSize)newSize {
  BOOL changed = !NSEqualSizes(self.frame.size, newSize);
  [super setFrameSize:newSize];
  if (changed) ++geometryRevision;
  [self layoutSurfaces];
}
- (void)viewDidChangeBackingProperties {
  [super viewDidChangeBackingProperties];
  ++geometryRevision;
  [self layoutSurfaces];
}
- (void)layoutSurfaces {
  for (NSString *kind in self.surfaces) {
    IRPGameWebViewSurface *surface = self.surfaces[kind];
    NSSize source = surface.viewport;
    if (source.width <= 0 || source.height <= 0) continue;
    CGFloat padding = std::min<CGFloat>(surface.padding, (std::min(source.width, source.height) - 1) / 2);
    NSSize content = NSMakeSize(source.width - padding * 2, source.height - padding * 2);
    // Fit once on attachment, then preserve CSS pixel size as content grows.
    CGFloat baseScale = surface.referenceScale;
    CGFloat scale = baseScale;
    if (surface.customized) {
      scale = baseScale * surface.layoutScale;
    }
    if (!surface.explicitBounds) surface.effectiveScale = baseScale > 0 ? scale / baseScale : 1;
    if (surface.explicitBounds)
      scale = surface.normalizedBounds.size.width * self.bounds.size.width / content.width;
    BOOL canDisplay = self.previewVisible && scale > 0;
    surface.webView.hidden = !canDisplay;
    if (!canDisplay) continue;
    NSSize size = NSMakeSize(content.width * scale, content.height * scale);
    CGFloat x = ([kind isEqualToString:@"hand"] || [kind hasPrefix:@"hand:"])
                    ? kInset
                    : self.bounds.size.width - kInset - size.width;
    CGFloat y = kInset;
    if (surface.customized) {
      x = std::clamp<CGFloat>(surface.normalizedCenter.x * self.bounds.size.width - size.width / 2,
                             -size.width * (1 - kMinimumVisibleFraction),
                             self.bounds.size.width - size.width * kMinimumVisibleFraction);
      y = std::clamp<CGFloat>((1 - surface.normalizedCenter.y) * self.bounds.size.height - size.height / 2,
                             -size.height * (1 - kMinimumVisibleFraction),
                             self.bounds.size.height - size.height * kMinimumVisibleFraction);
    }
    NSRect frame = NSMakeRect(x, y, size.width, size.height);
    if (surface.explicitBounds) {
      NSRect normalized = surface.normalizedBounds;
      frame = NSMakeRect(normalized.origin.x * self.bounds.size.width,
          (1 - normalized.origin.y - normalized.size.height) * self.bounds.size.height,
          normalized.size.width * self.bounds.size.width,
          normalized.size.height * self.bounds.size.height);
      size = frame.size;
    }
    surface.contentFrame = frame;
    CGFloat inset = padding * scale;
    NSRect webFrame = NSInsetRect(frame, -inset, -inset);
    // The clip sits at the web frame and the page fills it, so moving a viewer moves only the clip.
    if (!NSEqualRects(surface.clip.frame, webFrame)) surface.clip.frame = webFrame;
    NSRect pageFrame = surface.clip.bounds;
    if (!NSEqualRects(surface.webView.frame, pageFrame)) surface.webView.frame = pageFrame;
    if (padding > 0) {
      if (surface.contentMask == nil) surface.contentMask = [CAShapeLayer layer];
      NSRect maskRect = NSMakeRect(inset, inset, size.width, size.height);
      if (surface.webView.layer.mask != surface.contentMask ||
          !NSEqualRects(surface.contentMask.frame, surface.webView.bounds) ||
          !CGRectEqualToRect(CGPathGetBoundingBox(surface.contentMask.path), NSRectToCGRect(maskRect))) {
        CGPathRef path = CGPathCreateWithRect(NSRectToCGRect(maskRect), nullptr);
        [CATransaction begin];
        [CATransaction setDisableActions:YES];
        surface.contentMask.frame = surface.webView.bounds;
        surface.contentMask.path = path;
        surface.webView.layer.mask = surface.contentMask;
        [CATransaction commit];
        CGPathRelease(path);
      }
    } else surface.webView.layer.mask = nil;
    // Preserve the CSS viewport used by the desktop/OBS renderer while fitting
    // the visual surface into game points (independent of Retina backing scale).
    if (std::abs(surface.webView.pageZoom - scale) > 0.000001) surface.webView.pageZoom = scale;
    [surface applyCoversInHost:self.bounds.size];
  }
  self.chrome.hidden = !self.editing || !self.previewVisible;
  self.chrome.needsDisplay = self.editing;
  [self updateEditorMask];
}
- (void)setEditorExclusions:(const double *)values count:(int)count {
  std::vector<double> next;
  for (int i = 0; values != nullptr && i < count; i++) {
    const double *shape = values + i * 5;
    bool finite = true;
    for (int j = 0; j < 5; j++) finite = finite && std::isfinite(shape[j]);
    if (!finite || shape[2] <= 0 || shape[3] <= 0) continue;
    next.insert(next.end(), {shape[0], shape[1], shape[2], shape[3], std::max(0.0, shape[4])});
  }
  _editorExclusions = next;
  [self updateEditorMask];
}
- (void)updateEditorMask {
  if (!self.editing || _editorExclusions.empty()) {
    self.layer.mask = nil;
    _appliedMask.clear();
    return;
  }
  NSSize size = self.bounds.size;
  std::vector<double> applied;
  CGMutablePathRef path = CGPathCreateMutable();
  CGPathAddRect(path, nullptr, NSRectToCGRect(self.bounds));
  for (size_t i = 0; i + 4 < _editorExclusions.size(); i += 5) {
    const double *shape = _editorExclusions.data() + i;
    NSRect exclusion = NSIntersectionRect(self.bounds, NSMakeRect(
        shape[0] * size.width, (1 - shape[1] - shape[3]) * size.height,
        shape[2] * size.width, shape[3] * size.height));
    if (NSIsEmptyRect(exclusion)) continue;
    // Unity's rounded panels must not show square corners of game behind them.
    CGFloat radius = std::min({(CGFloat)(shape[4] * size.height), exclusion.size.width / 2,
                               exclusion.size.height / 2});
    if (radius > 0.5)
      CGPathAddRoundedRect(path, nullptr, NSRectToCGRect(exclusion), radius, radius);
    else
      CGPathAddRect(path, nullptr, NSRectToCGRect(exclusion));
    applied.insert(applied.end(), {exclusion.origin.x, exclusion.origin.y, exclusion.size.width,
                                   exclusion.size.height, radius});
  }
  if (applied.empty()) {
    self.layer.mask = nil;
    _appliedMask.clear();
    CGPathRelease(path);
    return;
  }
  if (self.layer.mask != nil && NSEqualRects(self.editorMask.frame, self.bounds) && applied == _appliedMask) {
    CGPathRelease(path);
    return;
  }
  if (self.editorMask == nil) {
    self.editorMask = [CAShapeLayer layer];
    self.editorMask.fillRule = kCAFillRuleEvenOdd;
    self.editorMask.fillColor = NSColor.whiteColor.CGColor;
  }
  // Disable implicit Core Animation actions: editor changes track the pointer
  // and window size exactly, without delayed mask interpolation.
  [CATransaction begin];
  [CATransaction setDisableActions:YES];
  self.editorMask.frame = self.bounds;
  self.editorMask.path = path;
  _appliedMask = applied;
  self.layer.mask = self.editorMask;
  [CATransaction commit];
  CGPathRelease(path);
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
  NSRect frame = GameRect(window.contentView.bounds);
  if (host == nil) host = [[IRPGameWebViewHost alloc] initWithFrame:frame];
  gameWindow = window;
  host.previewVisible = previewVisible;
  if (host.superview != window.contentView) {
    [host removeFromSuperview];
    host.frame = frame;
    [window.contentView addSubview:host positioned:NSWindowAbove relativeTo:nil];
  } else if (!NSEqualRects(host.frame, frame)) {
    // Resizing keeps the game's aspect; the autoresizing mask alone would stretch the host.
    host.frame = frame;
    ++geometryRevision;
  }
  [host layoutSurfaces];
  if (windowObservers == nil) {
    windowObservers = [NSMutableArray array];
    for (NSNotificationName name in @[
           NSWindowDidEnterFullScreenNotification,
           NSWindowDidExitFullScreenNotification,
           NSWindowDidResizeNotification,
           NSWindowDidChangeBackingPropertiesNotification,
           NSWindowDidChangeScreenNotification,
           NSWindowDidDeminiaturizeNotification
         ]) {
      id observer = [NSNotificationCenter.defaultCenter
          addObserverForName:name object:nil queue:NSOperationQueue.mainQueue
                  usingBlock:^(NSNotification *notification) {
                    if (notification.object == gameWindow) {
                      ++geometryRevision;
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
  [surface.clip removeFromSuperview];
  [host.surfaces removeObjectForKey:kind];
  host.chrome.needsDisplay = YES;
  SetFault(kind, "");
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
  if (!ValidKind(viewerKind)) {
    SetError("Native viewer identifier must begin with hand or foot.");
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
      surface.kind = kind;
      surface.referenceScale = std::min<CGFloat>(1, std::min(kMaximumWidth / width, kMaximumHeight / height));
      WKWebViewConfiguration *configuration = [[WKWebViewConfiguration alloc] init];
      configuration.websiteDataStore = WKWebsiteDataStore.nonPersistentDataStore;
      configuration.preferences.javaScriptCanOpenWindowsAutomatically = NO;
      IRPGameWebViewPaddingBridge *bridge = [[IRPGameWebViewPaddingBridge alloc] init];
      __weak IRPGameWebViewSurface *weakSurface = surface;
      __weak IRPGameWebViewHost *weakHost = host;
      bridge.changed = ^(CGFloat padding) {
        IRPGameWebViewSurface *current = weakSurface;
        if (current != nil && std::abs(current.padding - padding) > 0.001) {
          current.padding = padding;
          ++geometryRevision;
          [weakHost layoutSurfaces];
        }
      };
      [configuration.userContentController addScriptMessageHandler:bridge name:@"irpPadding"];
      // User scripts run in insertion order: the bridge must precede its callers.
      [configuration.userContentController addUserScript:PageScript(BridgeScript())];
      [configuration.userContentController addUserScript:PageScript(@(irp::scripts::kPadding))];
      IRPGameWebViewLayoutBridge *layoutBridge = [[IRPGameWebViewLayoutBridge alloc] init];
      layoutBridge.changed = ^(NSSize viewport, CGFloat padding) {
        IRPGameWebViewSurface *current = weakSurface;
        if (current == nil) return;
        current.reportedViewport = viewport;
        if (!NSEqualSizes(current.viewport, viewport) || std::abs(current.padding - padding) > 0.001) {
          current.viewport = viewport;
          current.padding = padding;
          current.selectionInViewport = NSZeroRect;
          ++geometryRevision;
          [weakHost layoutSurfaces];
        }
      };
      [configuration.userContentController addScriptMessageHandler:layoutBridge name:@"irpLayout"];
      [configuration.userContentController addUserScript:PageScript(@(irp::scripts::kLayout))];
      IRPGameWebViewSelectionBridge *selectionBridge = [[IRPGameWebViewSelectionBridge alloc] init];
      selectionBridge.changed = ^(NSRect bounds) { weakSurface.selectionInViewport = bounds; };
      [configuration.userContentController addScriptMessageHandler:selectionBridge name:@"irpSelection"];
      [configuration.userContentController addUserScript:PageScript(@(irp::scripts::kSelection))];
      surface.webView = [[IRPGameWebView alloc] initWithFrame:NSZeroRect configuration:configuration];
      surface.webView.navigationDelegate = surface;
      surface.webView.UIDelegate = surface;
      surface.webView.allowsBackForwardNavigationGestures = NO;
      surface.webView.allowsLinkPreview = NO;
      surface.webView.wantsLayer = YES;
      // WKWebView exposes transparent drawing via this WebKit property on macOS.
      // The outer exception boundary reports incompatibility without crashing Unity.
      [surface.webView setValue:@NO forKey:@"drawsBackground"];
      surface.clip = [[IRPGameWebViewClip alloc] initWithFrame:NSZeroRect];
      surface.clip.wantsLayer = YES;
      [surface.clip addSubview:surface.webView];
      [host addSubview:surface.clip positioned:NSWindowBelow relativeTo:host.chrome];
      host.surfaces[kind] = surface;
    }
    BOOL sourceChanged = ![surface.url isEqual:source];
    if (sourceChanged) surface.reportedViewport = NSZeroSize;
    // Descriptors bootstrap the page. Once it reports its computed layout,
    // repeated descriptors must not restore obsolete desktop window bounds.
    NSSize viewport = surface.reportedViewport.width > 0 ? surface.reportedViewport : NSMakeSize(width, height);
    if (!NSEqualSizes(surface.viewport, viewport)) ++geometryRevision;
    surface.viewport = viewport;
    surface.editing = host.editing;
    [host layoutSurfaces];
    if (sourceChanged) {
      if (surface.padding != 0) ++geometryRevision;
      surface.padding = 0;
      surface.selectionInViewport = NSZeroRect;
      surface.url = source;
      [host layoutSurfaces];
      SetFault(kind, "");
      [surface.webView loadRequest:[NSURLRequest requestWithURL:source
                                                  cachePolicy:NSURLRequestReloadIgnoringLocalCacheData
                                              timeoutInterval:10]];
    }
    // The call succeeded; earlier call errors no longer describe the host.
    SetError("");
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

extern "C" __attribute__((visibility("default"))) int irp_webview_set_layout(
    const char *viewerKind, int customized, double centerX, double centerY, double scale) {
  if (viewerKind == nullptr ||
      !std::isfinite(centerX) || !std::isfinite(centerY) || !std::isfinite(scale) || scale <= 0) {
    SetError("Native viewer layout requires a valid viewer and finite positive size.");
    return 0;
  }
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    if (surface == nil) return 0;
    surface.explicitBounds = NO;
    surface.customized = customized != 0;
    surface.normalizedCenter = NSMakePoint(std::clamp(centerX, -4.0, 5.0), std::clamp(centerY, -4.0, 5.0));
    surface.layoutScale = std::clamp(scale, 0.2, 5.0);
    [host layoutSurfaces];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_set_bounds(
    const char *viewerKind, double x, double y, double width, double height, double effectiveScale) {
  if (viewerKind == nullptr || !std::isfinite(x) || !std::isfinite(y) ||
      !std::isfinite(width) || !std::isfinite(height) || !std::isfinite(effectiveScale) ||
      width <= 0 || height <= 0 || effectiveScale <= 0) {
    SetError("Native viewer bounds require finite coordinates and positive size.");
    return 0;
  }
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    if (surface == nil) return 0;
    NSRect normalized = NSMakeRect(x, y, width, height);
    BOOL changed = !surface.explicitBounds || !NSEqualRects(surface.normalizedBounds, normalized) ||
        surface.effectiveScale != effectiveScale;
    surface.explicitBounds = YES;
    surface.normalizedBounds = normalized;
    surface.effectiveScale = effectiveScale;
    if (changed) [host layoutSurfaces];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_reference_size(
    const char *viewerKind, double *width, double *height) {
  if (viewerKind == nullptr || width == nullptr || height == nullptr) return 0;
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    if (surface == nil || surface.viewport.width <= 0 || surface.viewport.height <= 0) return 0;
    NSSize source = surface.viewport;
    CGFloat padding = std::min<CGFloat>(surface.padding, (std::min(source.width, source.height) - 1) / 2);
    CGFloat fit = surface.referenceScale;
    *width = (source.width - 2 * padding) * fit;
    *height = (source.height - 2 * padding) * fit;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_content_size(
    const char *viewerKind, double *width, double *height) {
  if (viewerKind == nullptr || width == nullptr || height == nullptr) return 0;
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    if (surface == nil || surface.viewport.width <= 0 || surface.viewport.height <= 0) return 0;
    NSSize source = surface.viewport;
    CGFloat padding = std::min<CGFloat>(surface.padding, (std::min(source.width, source.height) - 1) / 2);
    *width = source.width - 2 * padding;
    *height = source.height - 2 * padding;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_view_size(double *width, double *height) {
  if (width == nullptr || height == nullptr || NSApp == nil) return 0;
  return OnMainThread(^{
    // Before any viewer attaches there is no host yet; it will cover where the game is drawn.
    NSView *view = host != nil && host.window != nil ? host : FindGameWindow(gameWindow).contentView;
    if (view == nil || view.bounds.size.width <= 0 || view.bounds.size.height <= 0) return 0;
    NSSize size = view == host ? view.bounds.size : GameRect(view.bounds).size;
    *width = size.width;
    *height = size.height;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_game_size(double width, double height) {
  if (!std::isfinite(width) || !std::isfinite(height) || NSApp == nil) return;
  NSSize size = width > 0 && height > 0 ? NSMakeSize(width, height) : NSZeroSize;
  OnMainThread(^{
    if (NSEqualSizes(gameSize, size)) return 1;
    gameSize = size;
    ++geometryRevision;
    if (host != nil && host.superview != nil) {
      NSRect frame = GameRect(host.superview.bounds);
      if (!NSEqualRects(host.frame, frame)) host.frame = frame;
      [host layoutSurfaces];
    }
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) unsigned long long irp_webview_geometry_revision(void) {
  return geometryRevision.load(std::memory_order_relaxed);
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_bounds(
    const char *viewerKind, double *x, double *y, double *width, double *height, double *effectiveScale) {
  if (viewerKind == nullptr || x == nullptr || y == nullptr || width == nullptr ||
      height == nullptr || effectiveScale == nullptr) return 0;
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    NSSize size = host.bounds.size;
    if (surface == nil || size.width <= 0 || size.height <= 0) return 0;
    NSRect frame = surface.contentFrame;
    *x = NSMinX(frame) / size.width;
    *y = (size.height - NSMaxY(frame)) / size.height;
    *width = frame.size.width / size.width;
    *height = frame.size.height / size.height;
    *effectiveScale = surface.effectiveScale;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_selection_bounds(
    const char *viewerKind, double *x, double *y, double *width, double *height, double *effectiveScale) {
  if (viewerKind == nullptr || x == nullptr || y == nullptr || width == nullptr || height == nullptr || effectiveScale == nullptr) return 0;
  return OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    NSSize size = host.bounds.size;
    if (surface == nil || size.width <= 0 || size.height <= 0) return 0;
    NSRect measured = surface.selectionInViewport;
    NSRect frame = surface.contentFrame;
    if (measured.size.width > 0 && measured.size.height > 0) {
      // The page's viewport in host points: the clip holds it at its web frame.
      NSRect viewport = surface.clip.frame;
      frame = NSIntersectionRect(frame, NSMakeRect(
          NSMinX(viewport) + NSMinX(measured) * viewport.size.width,
          NSMaxY(viewport) - NSMaxY(measured) * viewport.size.height,
          measured.size.width * viewport.size.width, measured.size.height * viewport.size.height));
    }
    *x = NSMinX(frame) / size.width;
    *y = (size.height - NSMaxY(frame)) / size.height;
    *width = frame.size.width / size.width;
    *height = frame.size.height / size.height;
    *effectiveScale = surface.effectiveScale;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) int irp_webview_get_pointer(double *x, double *y) {
  if (x == nullptr || y == nullptr) return 0;
  return OnMainThread(^{
    if (host == nil || host.window == nil || host.bounds.size.width <= 0 || host.bounds.size.height <= 0) return 0;
    NSPoint windowPoint = [host.window convertPointFromScreen:NSEvent.mouseLocation];
    NSPoint point = [host convertPoint:windowPoint fromView:nil];
    *x = (point.x - NSMinX(host.bounds)) / host.bounds.size.width;
    *y = (NSMaxY(host.bounds) - point.y) / host.bounds.size.height;
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_editing(int editing) {
  OnMainThread(^{
    host.editing = editing != 0;
    for (IRPGameWebViewSurface *surface in host.surfaces.allValues) {
      surface.editing = host.editing;
      [surface.webView evaluateJavaScript:host.editing ? @"window.__irpSetEditing?.(true);" : @"window.__irpSetEditing?.(false);"
                       completionHandler:nil];
    }
    [host layoutSurfaces];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_selected(const char *viewerKind, int selected) {
  if (viewerKind == nullptr) return;
  OnMainThread(^{
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:viewerKind]];
    if (surface != nil && surface.selected != (selected != 0)) {
      surface.selected = selected != 0;
      host.chrome.needsDisplay = YES;
    }
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_highlight(double red, double green, double blue) {
  // Retain the export for managed callers that resolve this ABI. The OBS page
  // owns its skin: a mod accent must never override per-key colors or alpha.
  (void)red;
  (void)green;
  (void)blue;
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_editor_exclusion(
    double x, double y, double width, double height) {
  if (!std::isfinite(x) || !std::isfinite(y) || !std::isfinite(width) || !std::isfinite(height)) return;
  std::vector<double> shape{x, y, std::max(0.0, width), std::max(0.0, height), 0};
  OnMainThread(^{
    [host setEditorExclusions:shape.data() count:1];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_editor_exclusions(const double *values,
                                                                                        int count) {
  if (count < 0 || (count > 0 && values == nullptr)) return;
  std::vector<double> copy(values, values + (size_t)count * 5);
  OnMainThread(^{
    [host setEditorExclusions:copy.data() count:(int)(copy.size() / 5)];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_set_covers(const char *viewerKind,
                                                                             const double *values, int count) {
  if (!ValidKind(viewerKind) || count < 0 || count > kMaximumCovers || (count > 0 && values == nullptr)) return;
  std::string kind = viewerKind;
  std::vector<double> next;
  next.reserve((size_t)count * 4);
  for (int i = 0; i < count; i++) {
    const double *cover = values + (size_t)i * 4;
    bool finite = true;
    for (int j = 0; j < 4; j++) finite = finite && std::isfinite(cover[j]);
    if (!finite || cover[2] <= 0 || cover[3] <= 0) continue;
    next.insert(next.end(), cover, cover + 4);
  }
  OnMainThread(^{
    if (next.empty()) covers.erase(kind);
    else covers[kind] = next;
    IRPGameWebViewSurface *surface = host.surfaces[[NSString stringWithUTF8String:kind.c_str()]];
    [surface applyCoversInHost:host.bounds.size];
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) void irp_webview_dispose(void) {
  OnMainThread(^{
    for (id observer in windowObservers) {
      [NSNotificationCenter.defaultCenter removeObserver:observer];
    }
    windowObservers = nil;
    for (NSString *kind in host.surfaces.allKeys) RemoveSurface(kind);
    [host removeFromSuperview];
    host = nil;
    gameWindow = nil;
    previewVisible = true;
    covers.clear();
    ClearFaults();
    SetError("");
    return 1;
  });
}

extern "C" __attribute__((visibility("default"))) const char *irp_webview_last_error(void) {
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
