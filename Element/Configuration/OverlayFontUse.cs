using System.Collections.Generic;

namespace Element.Configuration;

/// <summary>
/// Where one font is set in the overlay: as its default font, in text runs, or
/// both. Changing a font through it touches exactly those places, so a font
/// picked to stand in for a missing one never moves texts that already used it.
/// </summary>
internal sealed class OverlayFontUse
{
  private OverlayFontUse(string key) => Key = key;

  /// <summary>The font as it was found, in saved form.</summary>
  public string Key { get; }

  public bool IsDefault { get; private set; }

  public List<OverlayTextRun> Runs { get; } = new();

  /// <summary>Sets <paramref name="key"/> everywhere this font was found; empty follows the default.</summary>
  public void Set(ElementSettings settings, string key)
  {
    key = OverlayFontKey.Normalize(key);
    if (IsDefault)
      settings.OverlayFont = key;
    foreach (OverlayTextRun run in Runs)
      run.Font = key;
  }

  /// <summary>Every font the overlay names, the default font first, then in text order.</summary>
  public static List<OverlayFontUse> Find(ElementSettings settings)
  {
    var uses = new List<OverlayFontUse>();
    string font = OverlayFontKey.Normalize(settings?.OverlayFont);
    if (font.Length > 0)
      Use(uses, font).IsDefault = true;
    if (settings?.OverlayLayouts == null)
      return uses;
    foreach (OverlayLayoutSettings layout in settings.OverlayLayouts)
      if (layout?.Text?.Runs != null)
        foreach (OverlayTextRun run in layout.Text.Runs)
        {
          string key = OverlayFontKey.Normalize(run?.Font);
          if (key.Length > 0)
            Use(uses, key).Runs.Add(run);
        }
    return uses;
  }

  private static OverlayFontUse Use(List<OverlayFontUse> uses, string key)
  {
    foreach (OverlayFontUse use in uses)
      if (OverlayFontKey.Same(use.Key, key))
        return use;
    var added = new OverlayFontUse(key);
    uses.Add(added);
    return added;
  }
}
