using UnityEngine;

namespace Element.Presentation.Ui.Theme;

/// <summary>Picks the Korean or English wording, following the game's language.</summary>
internal static class UiLocale
{
  public static bool Korean => RDString.language == SystemLanguage.Korean;

  // Pure for ElementUI: screens may call it in watched expressions; a language change mounts them again.
  [ElementUI.Pure]
  public static string L(string ko, string en) => Korean ? ko : en;

  /// <summary>The same choice for a language the caller already knows.</summary>
  [ElementUI.Pure]
  public static string L(bool korean, string ko, string en) => korean ? ko : en;
}
