using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// What a group is called on screen: the name typed for it, or for a
/// built-in group its name in the player's language.
/// </summary>
internal static class GroupNames
{
  public static string Of(IReadOnlyList<OverlayLayoutSettings> layouts, string group)
  {
    if (string.IsNullOrEmpty(group))
      return string.Empty;
    if (layouts != null)
      foreach (OverlayLayoutSettings layout in layouts)
        if (layout != null && layout.Group == group && !string.IsNullOrEmpty(layout.GroupName))
          return layout.GroupName;
    return group switch
    {
      OverlayElementDefaults.ProgressBarGroup => L("진행도 바", "Progress bar"),
      OverlayElementDefaults.ComboGroup => L("콤보", "Combo"),
      _ => L("묶음", "Group"),
    };
  }
}
