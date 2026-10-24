#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// This ABI requires macOS 11 or later. AppKit work is marshalled to the main
// thread. Keep the dylib loaded until process exit, including across mod reloads.
int irp_webview_supported(void);

// width/height are the original CSS viewport dimensions, not Retina pixels.
// Initial display fits within 430x270 points. That reference scale stays fixed
// as content grows. A null/empty URL removes only this viewer.
// viewer_kind is "hand", "foot", or "hand:<instance>" / "foot:<instance>".
// Every instance owns independent geometry and lifetime. Success means attached and navigation
// scheduled; it does not confirm page load or inclusion in a capture source.
int irp_webview_set_surface(const char *viewer_kind, const char *url, int width,
                            int height);
void irp_webview_set_visible(int visible);
// Layout centers are normalized to the game content view, with a top-left
// origin. Scale uniformly multiplies the normal fitted viewer size. Passing
// customized=0 restores automatic bottom placement. No page reload occurs.
int irp_webview_set_layout(const char *viewer_kind, int customized,
                           double center_x, double center_y, double scale);
// New layouts are resolved in shared managed game-viewport coordinates. This
// receives the final cropped content rectangle normalized to that viewport;
// AppKit only converts it to view points and preserves the CSS viewport.
int irp_webview_set_bounds(const char *viewer_kind, double x, double y,
                           double width, double height, double effective_scale);
// Cropped content size in 1920x1080 reference units, multiplied by the initial
// attachment's fixed fit factor. Independent of host size and Retina backing.
int irp_webview_get_reference_size(const char *viewer_kind, double *width, double *height);
// Changes only when external geometry changes (source, padding, host, backing),
// never for set_bounds. Safe to poll without dispatching to the AppKit thread.
unsigned long long irp_webview_geometry_revision(void);
// Actual normalized top-left bounds, including any viewport-size clamping.
// effective_scale reports the actual multiplier of the normal fitted size.
int irp_webview_get_bounds(const char *viewer_kind, double *x, double *y,
                           double *width, double *height,
                           double *effective_scale);
// Tight visible key/stat content, excluding the reserved note animation area.
// Uses the same coordinate space as get_bounds. Selected editing uses get_bounds.
int irp_webview_get_selection_bounds(const char *viewer_kind, double *x, double *y,
                                     double *width, double *height,
                                     double *effective_scale);
// Current pointer in the exact same normalized top-left host space as bounds.
// This avoids Unity/AppKit drawable-origin differences (title bars, Retina).
int irp_webview_get_pointer(double *x, double *y);
// Draw click-through white dashed bounds and handles above WebKit content.
void irp_webview_set_editing(int editing);
// Native owns the only selection outline for this viewer; Unity does not draw it.
void irp_webview_set_selected(const char *viewer_kind, int selected);
// Compatibility no-op. OBS key/stat/graph colors and alpha belong to the page,
// not the mod's highlight theme. Kept for existing managed ABI callers.
void irp_webview_set_highlight(double red, double green, double blue);
// Cut out a normalized top-left rectangle from all native content/chrome during
// editing so Unity's layout toolbar stays visible above freely moved viewers.
void irp_webview_set_editor_exclusion(double x, double y, double width, double height);
void irp_webview_dispose(void);

// Thread-local copy; valid until this thread next calls last_error. Diagnostics
// never contain a URL or its authentication token.
const char *irp_webview_last_error(void);

#ifdef __cplusplus
}
#endif
