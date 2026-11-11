using Element.Presentation.Ui.Theme;

namespace Element.Presentation.Layout.KeyViewers;

/// <summary>What the layout editor calls each kind of ElementNote key viewer.</summary>
internal static class KeyViewerLabels
{
  public static string For(string kind) =>
    kind == "hand" ? UiLocale.L("손 키뷰어", "Hand keyviewer") : UiLocale.L("발 키뷰어", "Foot keyviewer");
}
