using Element.Domain.Overlay.Fonts;
using TMPro;

namespace Element.Application.Fonts;

/// <summary>Finds a saved font on this PC and loads it for TextMeshPro.</summary>
internal interface IOverlayFontSource
{
  /// <summary>Changes when fonts become findable: the installed list arrived or a file was imported.</summary>
  int Revision { get; }

  OverlayFontLookup Find(string key, out OverlayFontFace face);

  /// <summary>A dynamic font asset for the face, or null when it can't be read.</summary>
  TMP_FontAsset Create(OverlayFontFace face);

  /// <summary>
  /// The face has no basic Latin letters or digits of its own, as many Hebrew,
  /// Arabic and Thai fonts don't. Main thread.
  /// </summary>
  bool LacksLatin(OverlayFontFace face);

  /// <summary>
  /// The font the OS draws such a face's Latin letters with (Helvetica on
  /// macOS, Segoe UI on Windows) in <paramref name="style"/> or its regular
  /// face; null when this PC has none.
  /// </summary>
  TMP_FontAsset CreateLatinFallback(string style);

  /// <summary>Takes the installed list once it is ready. Main thread, once per frame at most.</summary>
  void Poll();
}
