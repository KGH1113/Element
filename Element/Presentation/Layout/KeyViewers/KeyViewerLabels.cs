using Element.Presentation.Settings;

namespace Element.Presentation.Layout.KeyViewers;

/// <summary>What the layout editor calls each kind of ElementNote key viewer.</summary>
internal static class KeyViewerLabels
{
  public static string For(string kind) =>
    kind == "hand" ? SettingsLocale.L("손 키뷰어", "Hand keyviewer") : SettingsLocale.L("발 키뷰어", "Foot keyviewer");
}
