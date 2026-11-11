namespace Element.Configuration.Overlay;

public static class OverlayElementKinds
{
  public const string Text = "text";
  public const string Shape = "shape";
  public const string HandKeyViewer = "native-hand";
  public const string FootKeyViewer = "native-foot";

  public static bool IsKeyViewer(string templateId) => templateId == HandKeyViewer || templateId == FootKeyViewer;

  /// <summary>An element another mod draws through ElementKit: "ModId/type".</summary>
  public static bool IsMod(string templateId) => !string.IsNullOrEmpty(templateId) && templateId.IndexOf('/') > 0;

  /// <summary>The id of the mod that draws <paramref name="templateId"/>; empty for Element's own kinds.</summary>
  public static string ModOf(string templateId) =>
    IsMod(templateId) ? templateId.Substring(0, templateId.IndexOf('/')) : string.Empty;
}
