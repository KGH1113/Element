using Element.Configuration;
using Element.Domain.Layout;

namespace Element.Domain.DmNote;

/// <summary>
/// Where a key viewer sits until the player moves it, and after a reset: full
/// size (one CSS pixel per point or DIP) on the bottom left, the hand viewer
/// raised from the bottom edge and the foot viewer to its right near the edge.
/// Offsets are reference units, like any anchored overlay element.
/// </summary>
internal static class NativeViewerDefaultLayout
{
  /// <summary>A new anchored placement for a "hand" or "foot" viewer.</summary>
  public static OverlayLayoutSettings For(string kind)
  {
    bool foot = kind == "foot";
    return new OverlayLayoutSettings
    {
      LayoutVersion = OverlayLayoutPosition.CurrentVersion,
      Anchor = Anchor9.BottomLeft,
      OffsetX = foot ? 425.44f : 0,
      OffsetY = foot ? -4.375f : -202.04f,
      Scale = 1,
    };
  }
}
