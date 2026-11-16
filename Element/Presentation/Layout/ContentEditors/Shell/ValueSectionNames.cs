using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>The names the input dock shows: its value sections' headings and the values in them.</summary>
internal static class ValueSectionNames
{
  /// <summary>A dock heading: a built-in category, or the heading another mod gave its values.</summary>
  public static string SectionName(OverlayValueSection section) =>
    section.Group != null
      ? UiLocale.L(section.Group.KoreanName, section.Group.EnglishName)
      : CategoryName(section.Category);

  public static string CategoryName(OverlayValueCategory category) =>
    category switch
    {
      OverlayValueCategory.Progress => UiLocale.L("진행", "Progress"),
      OverlayValueCategory.Judgement => UiLocale.L("판정", "Judgment"),
      OverlayValueCategory.Time => UiLocale.L("시간", "Time"),
      OverlayValueCategory.Speed => UiLocale.L("속도", "Speed"),
      OverlayValueCategory.Song => UiLocale.L("곡·기록", "Song"),
      _ => UiLocale.L("시스템", "System"),
    };

  /// <summary>A value's name in the language shown: on its chip, in a formula, on a dragged value.</summary>
  public static string ValueName(OverlayValueDefinition value) =>
    UiLocale.Korean ? value.KoreanName : value.EnglishName;
}
