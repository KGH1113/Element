# Native game webview (macOS and Windows)

## Overview

`GameWebView.mm` embeds a transparent, click-through `WKWebView` for each added hand or foot element
in the Unity game's existing `NSWindow.contentView`. It creates no extra native
window. ImplDmNote supplies authenticated local OBS viewer URLs and original CSS
viewport dimensions. WebKit renders those pages directly; Unity does not copy
screen captures or upload a texture. This is the only in-game viewer path;
`GameWebViewWin.cpp` implements the same ABI on Windows (see [Windows](#windows)).
Linux does not yet have a native host.

Both hosts inject the page scripts in `ViewerScripts.h`. Each first defines
`window.__irpPost(name, value)` for its own bridge (WebKit message handlers or
WebView2 web messages) and validates every value it receives.

## Security

The host accepts only explicit-port loopback HTTP URLs, keeps navigation within
the supplied origin, uses nonpersistent WebKit storage, and excludes URLs and
authentication tokens from diagnostics. The renderer remains a browser and can
load its page's resources and plugins; this is not a network sandbox.

## Placement

On initial attachment, each viewer fits within 430×270 game-window points with 16-point bottom and side
insets. That initial reference scale is frozen: larger content grows the view,
and neither larger content nor narrower windows trigger another fit. `pageZoom`
preserves the original CSS viewport while scaling the native view. AppKit owns
window movement and autoresizing. Full-screen transition notifications reattach
the host if Unity replaces its content view. All AppKit operations execute on
the main thread.

Edited native layouts store one of five screen anchors (four corners and center), reference-unit offsets between
the matching element/screen anchors, and an individual uniform size multiplier.
The shared C# resolver uses the actual Unity game viewport and
`s = sqrt((width / 1920) * (height / 1080))` for both size and offsets. The native
reference size is the cropped CSS viewport multiplied by
the fixed `min(1, 430 / initialCssWidth, 270 / initialCssHeight)`. Native code receives the final normalized
content rectangle and converts it to AppKit points, avoiding a second DPI scale.
Legacy normalized centers keep their original rendering until an explicit edit
captures the visible rectangle; simply loading or viewing settings never migrates
the saved geometry. Reset restores the legacy/default placement.

The managed resolver compares the viewer center's actual pixel distance to the
five screen points after a move. The matching viewer corner or center remains
fixed during growth and shrinkage, and only another move reselects it. Temporary
anchored geometry also covers default/legacy viewers without rewriting their
saved settings. Unchanged settings, descriptor refreshes, and content changes do
not recapture that geometry; undo/redo restores the appropriate placement.
Unity overlays continue to use their existing nine-anchor policy.

Layout changes adjust the frame and `pageZoom` locally without reloading the page
or sending IPC. Runtime bounds retain at least 10% of both dimensions inside the
game window without overwriting saved offsets or scale. The disconnected editor
preview retains the last known native reference size. A click-through native chrome view draws
one rounded white dashed outline and eight handles for the selected viewer only;
Unity suppresses its duplicate outline. Unity owns pointer hit testing and
cursor feedback; AppKit supplies the pointer in the same host coordinate space
as the border, including window/titlebar offsets and Retina scaling. Settings
temporarily hide the native children so they cannot
cover the settings panel. During editing a host-level even-odd layer mask cuts out
the editor's toolbar bounds, keeping Unity's save/cancel controls visible even
when a viewer is moved over them. The exclusion follows toolbar dragging in the
same frame, follows window resizing, and is
removed when editing ends.

## Content size

The native page reads `window.api.settings` after bootstrap and subscribes to its
padding changes. A local layer mask crops the configured outer padding, and the
same cropped rectangle drives selection and hit testing. The original CSS viewport
and note track area remain intact; desktop and OBS settings are never modified.

The OBS renderer publishes its computed CSS width, height, and padding on the
`#root` element as `data-dmn-viewer-width`, `data-dmn-viewer-height`, and
`data-dmn-viewer-padding`. An attribute-filtered observer forwards only changed
layout dimensions to WebKit's `irpLayout` handler. Live dimensions replace the
initial descriptor size, so switching tabs, changing key layouts, and adjusting
note tracks resizes the view without reloading it or resetting input state.
Descriptor updates (pushed by ImplDmNote, or the occasional fallback describe)
cannot overwrite these dimensions with stale desktop bounds, and an unchanged
descriptor is not applied again.
A source URL change resets the live size until the new page reports its layout.
Geometry revisions let the managed resolver preserve saved anchors and scale
while updating content bounds. Key highlights and note animation do not trigger
size measurements or layout messages.

## C ABI

The public C ABI is in `GameWebView.h`. `irp_webview_set_bounds` accepts the resolved
normalized content rectangle; `irp_webview_get_reference_size` returns the cropped
reference-unit size. `irp_webview_geometry_revision` is an atomic, main-thread-free
poll for source viewport, padding, host size, and backing changes. Local position
updates do not increment this revision. `irp_webview_set_layout` remains the legacy
center/scale path. `irp_webview_set_surface` returns success
when the host is attached and navigation is scheduled, rather than waiting for
page load. A null or empty URL removes only that viewer. `dispose` removes views,
delegates, and observers; keep the dylib loaded until game process exit because
Objective-C classes and late WebKit callbacks outlive a managed mod reload.

## Verification

Build and run the standalone probe through `./scripts/run.sh native-webview-check`.
`GameWebViewSmoke.mm` creates a synthetic `PlayerWindow`/`PlayerWindowView` pair
using the native class names found in ADOFAI's installed UnityPlayer binary, and
a local HTTP fixture;
it checks the ABI, source validation, attachment, CSS viewport, transparency,
input pass-through, resizing, legacy and resolved layout, reference sizing,
Retina conversion, scale-only changes, aspect-ratio preservation,
selection ownership, immediate toolbar exclusion, padding crop, URL reuse, hiding,
removal, and lifecycle. At fractional zoom, WebKit can round its CSS layout viewport
by less than one AppKit point; the probe allows that physical rounding while
checking exact native content bounds and page reuse. Its host
is a test window, so passing this probe does not confirm the real Unity view
hierarchy or capture behavior.

In the running game, use the viewer preview to verify movement, resizing,
full screen, minimizing/restoring, gameplay
keyboard/mouse input, and enabling/disabling the mod. Then select only the game
in Discord sharing and OBS Window Capture and inspect the received/recorded
image. Capture backends can omit native child views even when the local game
window displays them. On 2026-10-26 Discord and OBS Window Capture both included
the viewers with only the game window selected; recheck after capture app or
macOS updates.

## Windows

`GameWebViewWin.cpp` builds `ImplGameWebView.dll`. Each viewer is a WebView2
composition controller whose root visual sits in a DirectComposition tree bound
to the Unity window (`UnityWndClass`) as a topmost target, so pages composite
above the game's swap chain inside the game window. No window covers the game
(WebView2's own helper child is 0×0) and no input is forwarded, so clicks, keys
and keyboard focus stay with Unity. Page rendering runs in `msedgewebview2.exe`.

All exports run on the Unity window's thread, which is a COM single-threaded
apartment; Unity's message pump also delivers WebView2 callbacks and the host's
timers. The host never subclasses Unity's window: `irp_webview_geometry_revision`,
which the managed host polls once per frame, notices size, DPI and minimize
changes. Hidden or minimized pages call `put_IsVisible(FALSE)`, which stops
rendering while keeping the page loaded.

Placement mirrors the macOS host in physical pixels: automatic layout measures
the 16-point inset and 430×270 fit in DIPs, explicit bounds come normalized from
the managed resolver, and `RasterizationScale` (with monitor scale detection off)
maps CSS pixels to pixels, so the page keeps its CSS viewport and rasterizes at
its final size. Offsets are rounded to whole pixels for sharp text. A visual clip
crops the configured padding. Editing chrome is drawn with Direct2D on a
DirectComposition surface; the toolbar exclusion clears that region of the chrome
and cuts the page with a CSS `clip-path` (even-odd path) because visual clips
cannot express holes.

Security matches macOS: explicit-port loopback HTTP only, top-level and frame
navigation limited to the descriptor's origin, new windows and script dialogs
blocked, InPrivate storage, and no URLs or tokens in diagnostics.

When a renderer exits the page reloads after one second; when the browser process
exits the environment and pages are rebuilt (at most five recoveries per minute,
after which the next descriptor retries). The WebView2 environment and graphics
devices outlive a disposed host for 60 seconds so the next gameplay session
attaches in about 3 ms instead of about 40 ms. The DLL statically links the CRT
and `WebView2LoaderStatic.lib` and depends only on system libraries; it requires
the Microsoft Edge WebView2 Runtime.

Capture: window capture through Windows Graphics Capture (OBS "Windows 10 (1903
and up)") and display capture include the viewers. OBS's automatic window capture
method selects BitBlt for Unity windows, and BitBlt and OBS Game Capture copy only
the game's frame, so they omit the viewers.

`./scripts/run.sh windows-webview-check` builds the DLL on the Windows host from
`.env` (MSVC, pinned WebView2 SDK) and runs `GameWebViewWinSmoke.cpp` on its signed-in
desktop. The probe creates a `UnityWndClass` window presenting a D3D11 flip-model
swap chain, serves a loopback fixture page, and checks composed screen pixels,
transparency, click and focus pass-through, editing chrome and exclusion,
visibility, content growth, stale descriptors, resize and minimize, navigation
blocking, browser-process recovery, and reattachment. Like the macOS probe, it
does not prove the real game's hierarchy or capture behavior.
