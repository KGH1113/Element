# Native game webview (macOS)

`GameWebView.mm` embeds up to two transparent, click-through `WKWebView` children
in the Unity game's existing `NSWindow.contentView`. It creates no extra native
window. ImplDmNote supplies authenticated local OBS viewer URLs and original CSS
viewport dimensions. WebKit renders those pages directly; Unity does not copy
screen captures or upload a texture. This is the only in-game viewer path;
Windows and Linux do not yet have a native host.

The host accepts only explicit-port loopback HTTP URLs, keeps navigation within
the supplied origin, uses nonpersistent WebKit storage, and excludes URLs and
authentication tokens from diagnostics. The renderer remains a browser and can
load its page's resources and plugins; this is not a network sandbox.

Both viewers fit within 430×270 game-window points with 16-point bottom and side
insets. Narrow windows give each viewer a separate half-width slot. `pageZoom`
preserves the original CSS viewport while scaling the native view. AppKit owns
window movement and autoresizing. Full-screen transition notifications reattach
the host if Unity replaces its content view. All AppKit operations execute on
the main thread.

The public C ABI is in `GameWebView.h`. `irp_webview_set_surface` returns success
when the host is attached and navigation is scheduled, rather than waiting for
page load. A null or empty URL removes only that viewer. `dispose` removes views,
delegates, and observers; keep the dylib loaded until game process exit because
Objective-C classes and late WebKit callbacks outlive a managed mod reload.

Build and run the standalone probe through `./scripts/run.sh native-webview-check`.
`GameWebViewSmoke.mm` creates a synthetic `PlayerWindow`/`PlayerWindowView` pair
using the native class names found in ADOFAI's installed UnityPlayer binary, and
a local HTTP fixture;
it checks the ABI, source validation, attachment, CSS viewport, transparency,
input pass-through, resizing, URL reuse, hiding, removal, and lifecycle. Its host
is a test window, so passing this probe does not confirm the real Unity view
hierarchy or capture behavior.

In the running game, use the viewer preview to verify movement, resizing,
full screen, minimizing/restoring, gameplay
keyboard/mouse input, and enabling/disabling the mod. Then select only the game
in Discord sharing and OBS Window Capture and inspect the received/recorded
image. Capture backends can omit native child views even when the local game
window displays them; capture inclusion remains to be verified on real clients.
