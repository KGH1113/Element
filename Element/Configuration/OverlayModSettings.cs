using System;

namespace Element.Configuration;

// An element another mod draws (TemplateId "ModId/key"): the settings that
// mod keeps for it, as the text it wrote. Element never reads them, so a mod
// that isn't installed leaves them as they are and reinstalling brings the
// element back as it was.
[Serializable]
[CopiedInPlace]
public sealed class OverlayModSettings
{
  public string Data = string.Empty;

  // The size the element last drew at, in pixels at 1080p, and its names:
  // what the editor shows while the mod that draws it is missing. Not an
  // edit, so undo and the editor's change checks ignore them.
  public float Width;
  public float Height;
  public string KoreanName = string.Empty;
  public string EnglishName = string.Empty;

  public OverlayModSettings Clone() =>
    new()
    {
      Data = Data ?? string.Empty,
      Width = Width,
      Height = Height,
      KoreanName = KoreanName ?? string.Empty,
      EnglishName = EnglishName ?? string.Empty,
    };

  /// <summary>Whether both hold the same settings.</summary>
  public bool SameAs(OverlayModSettings other) =>
    other != null && (Data ?? string.Empty) == (other.Data ?? string.Empty);
}
