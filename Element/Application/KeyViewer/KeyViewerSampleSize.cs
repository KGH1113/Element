namespace Element.Application.KeyViewer;

/// <summary>
/// The size of the key viewer sample, in reference units: the size a viewer
/// takes before ElementNote reports its page's own.
/// </summary>
internal static class KeyViewerSampleSize
{
  public const float ViewportHeight = 270;

  public static float Width(string kind) => kind == "hand" ? 430 : 210;
}
