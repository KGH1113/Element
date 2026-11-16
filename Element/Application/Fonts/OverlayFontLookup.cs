namespace Element.Application.Fonts;

internal enum OverlayFontLookup
{
  Found,

  // Installed fonts are still being listed; ask again when Revision changes.
  Pending,
  Missing,
}
