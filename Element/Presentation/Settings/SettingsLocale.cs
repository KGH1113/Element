using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>Picks the Korean or English wording, following the game's language.</summary>
internal static class SettingsLocale
{
  public static bool Korean => RDString.language == SystemLanguage.Korean;

  public static string L(string ko, string en) => Korean ? ko : en;

  /// <summary>For text chosen before the game's language is known, as in tests.</summary>
  public static string L(bool korean, string ko, string en) => korean ? ko : en;
}
