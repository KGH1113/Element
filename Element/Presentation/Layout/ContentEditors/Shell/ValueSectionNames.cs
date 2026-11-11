using Element.Presentation.Settings;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>The names of the input dock's value sections.</summary>
internal static class ValueSectionNames
{
  /// <summary>A dock heading: a built-in category, or the heading another mod gave its values.</summary>
  public static string SectionName(Domain.Elements.OverlayValueSection section) =>
    section.Group != null
      ? SettingsLocale.L(section.Group.KoreanName, section.Group.EnglishName)
      : CategoryName(section.Category);

  public static string CategoryName(Domain.Elements.OverlayValueCategory category) =>
    category switch
    {
      Domain.Elements.OverlayValueCategory.Progress => SettingsLocale.L("진행", "Progress"),
      Domain.Elements.OverlayValueCategory.Judgement => SettingsLocale.L("판정", "Judgment"),
      Domain.Elements.OverlayValueCategory.Time => SettingsLocale.L("시간", "Time"),
      Domain.Elements.OverlayValueCategory.Speed => SettingsLocale.L("속도", "Speed"),
      Domain.Elements.OverlayValueCategory.Song => SettingsLocale.L("곡·기록", "Song"),
      _ => SettingsLocale.L("시스템", "System"),
    };
}
