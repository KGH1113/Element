using Element.Presentation.Settings;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>The names of the input dock's value sections.</summary>
internal static class ValueSectionNames
{
  /// <summary>A dock heading: a built-in category, or the heading another mod gave its values.</summary>
  public static string SectionName(Domain.Overlay.Values.OverlayValueSection section) =>
    section.Group != null
      ? SettingsLocale.L(section.Group.KoreanName, section.Group.EnglishName)
      : CategoryName(section.Category);

  public static string CategoryName(Domain.Overlay.Values.OverlayValueCategory category) =>
    category switch
    {
      Domain.Overlay.Values.OverlayValueCategory.Progress => SettingsLocale.L("진행", "Progress"),
      Domain.Overlay.Values.OverlayValueCategory.Judgement => SettingsLocale.L("판정", "Judgment"),
      Domain.Overlay.Values.OverlayValueCategory.Time => SettingsLocale.L("시간", "Time"),
      Domain.Overlay.Values.OverlayValueCategory.Speed => SettingsLocale.L("속도", "Speed"),
      Domain.Overlay.Values.OverlayValueCategory.Song => SettingsLocale.L("곡·기록", "Song"),
      _ => SettingsLocale.L("시스템", "System"),
    };
}
