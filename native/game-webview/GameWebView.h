#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// This ABI requires macOS 11 or later. AppKit work is marshalled to the main
// thread. Keep the dylib loaded until process exit, including across mod reloads.
int irp_webview_supported(void);

// width/height are the original CSS viewport dimensions, not Retina pixels.
// Display fits within 430x270 points. A null/empty URL removes only this viewer.
// viewer_kind is exactly "hand" or "foot". Success means attached and navigation
// scheduled; it does not confirm page load or inclusion in a capture source.
int irp_webview_set_surface(const char *viewer_kind, const char *url, int width,
                            int height);
void irp_webview_set_visible(int visible);
void irp_webview_dispose(void);

// Thread-local copy; valid until this thread next calls last_error. Diagnostics
// never contain a URL or its authentication token.
const char *irp_webview_last_error(void);

#ifdef __cplusplus
}
#endif
