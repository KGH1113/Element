using Element.Domain.Overlay.Values;
using Element.Presentation.Ui.Theme;

namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>The names the input dock shows: its places (a category or a mod), a mod's group headings and the values.</summary>
internal static class ValueSectionNames
{
  /// <summary>A place in the dock's list: a built-in category, or a mod by the name players know it by.</summary>
  public static string FolderName(OverlayValueFolder folder) => UiLocale.L(folder.KoreanName, folder.EnglishName);

  /// <summary>The heading a mod gave a group of its values.</summary>
  public static string GroupName(OverlayValueGroup group) => UiLocale.L(group.KoreanName, group.EnglishName);

  /// <summary>A value's name in the language shown: on its chip, in a formula, on a dragged value.</summary>
  public static string ValueName(OverlayValueDefinition value) =>
    UiLocale.Korean ? value.KoreanName : value.EnglishName;
}
