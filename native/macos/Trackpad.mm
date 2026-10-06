#import <AppKit/AppKit.h>
#include <deque>
#include <mutex>

// C ABI: normalized view coordinates/deltas keep Retina and window resizing
// independent of the Unity render resolution. No Unity calls on this thread.
struct TrackpadEvent {
  int kind, phase, momentum, reserved;
  double x, y, dx, dy, magnification;
};
static_assert(sizeof(TrackpadEvent) == 56, "Managed event ABI must match");
static id monitor;
static bool enabled;
static bool overflow;
static std::mutex gate;
static std::deque<TrackpadEvent> events;

extern "C" __attribute__((visibility("default"))) void irp_trackpad_enable(int value) {
  std::lock_guard<std::mutex> lock(gate);
  if (enabled != (value != 0)) {
    events.clear();
    overflow = false;
    enabled = value != 0;
  }
}

extern "C" __attribute__((visibility("default"))) int irp_trackpad_start() {
  if (![NSThread isMainThread]) return 0;
  if (monitor) return 1;
  NSEventMask mask = NSEventMaskScrollWheel | NSEventMaskMagnify |
    NSEventMaskLeftMouseDown | NSEventMaskRightMouseDown |
    NSEventMaskOtherMouseDown | NSEventMaskKeyDown;
  monitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask handler:^NSEvent *(NSEvent *event) {
    std::lock_guard<std::mutex> lock(gate);
    // AppKit also delivers scroll/gesture events to an unfocused window under
    // the pointer. Accept those events without activating the application.
    if (!enabled || !event.window) return event;
    TrackpadEvent sample = {};
    if (event.type == NSEventTypeMagnify) {
      sample.kind = 2;
      sample.magnification = event.magnification;
    } else if (event.type == NSEventTypeScrollWheel) {
      // Precise deltas include Magic Mouse. Phase information is retained
      // instead of guessing a physical device from delta magnitude.
      if (!event.hasPreciseScrollingDeltas) return event;
      sample.kind = 1;
      sample.momentum = (int)event.momentumPhase;
    } else {
      sample.kind = 3;
    }
    if (sample.kind != 3) {
      NSView *view = event.window.contentView;
      NSRect bounds = view.bounds;
      if (bounds.size.width <= 0 || bounds.size.height <= 0) return event;
      NSPoint p = [view convertPoint:event.locationInWindow fromView:nil];
      sample.phase = (int)event.phase;
      sample.x = (p.x - bounds.origin.x) / bounds.size.width;
      sample.y = (p.y - bounds.origin.y) / bounds.size.height;
      if (view.isFlipped) sample.y = 1 - sample.y;
      if (sample.kind == 1) {
        sample.dx = event.scrollingDeltaX / bounds.size.width;
        // Scroll deltas describe content motion in a top-down screen frame;
        // Unity ScreenToWorldPoint uses a bottom-up screen frame.
        sample.dy = -event.scrollingDeltaY / bounds.size.height;
      }
    }
    if (events.size() >= 4096) {
      events.clear();
      overflow = true;
    }
    events.push_back(sample);
    // UI still receives ordinary scrolling. The managed camera/UI hooks
    // suppress only the legacy scroll belonging to a canvas gesture.
    return event;
  }];
  return monitor != nil;
}

extern "C" __attribute__((visibility("default"))) int irp_trackpad_drain(TrackpadEvent *output, int capacity) {
  std::lock_guard<std::mutex> lock(gate);
  if (overflow) {
    events.clear();
    overflow = false;
    return -1;
  }
  int count = 0;
  while (count < capacity && !events.empty()) {
    output[count++] = events.front();
    events.pop_front();
  }
  return count;
}

extern "C" __attribute__((visibility("default"))) void irp_trackpad_stop() {
  if (![NSThread isMainThread]) return;
  if (monitor) [NSEvent removeMonitor:monitor];
  monitor = nil;
  irp_trackpad_enable(0);
}
