using System;
using Element.Configuration;

namespace Element.Domain.Layout;

internal readonly struct AnchoredLayoutPosition
{
  public readonly Anchor9 Anchor;
  public readonly float OffsetX;
  public readonly float OffsetY;
  public bool IsValid =>
    OverlayLayoutPosition.ValidAnchor(Anchor)
    && OverlayLayoutGeometry.Finite(OffsetX)
    && OverlayLayoutGeometry.Finite(OffsetY);

  public AnchoredLayoutPosition(Anchor9 anchor, float offsetX, float offsetY)
  {
    Anchor = anchor;
    OffsetX = offsetX;
    OffsetY = offsetY;
  }
}

// Persist offsets in shared logical units, then resolve against the current
// element size. Coordinates are game-viewport pixels with a top-left origin.
//
// The anchor is a point on the screen; the offset runs from it to the same
// point of the element, its pivot. The pivot is the anchor's own point unless
// the element grows from another (OverlayGrowth), so a size change keeps the
// pivot where it is.
internal static class OverlayLayoutPosition
{
  public const int CurrentVersion = 1;
  public const float ReferenceWidth = 1920;
  public const float ReferenceHeight = 1080;

  // Matches CanvasScaler's logarithmic width/height blend with a 0.5 match value.
  public static float ScreenScale(float width, float height) =>
    Positive(width) && Positive(height)
      ? (float)Math.Sqrt((double)width / ReferenceWidth * ((double)height / ReferenceHeight))
      : 1;

  public static bool IsAnchored(OverlayLayoutSettings settings) =>
    settings != null
    && settings.LayoutVersion == CurrentVersion
    && ValidAnchor(settings.Anchor)
    && OverlayLayoutGeometry.Finite(settings.OffsetX)
    && OverlayLayoutGeometry.Finite(settings.OffsetY);

  public static bool ValidAnchor(Anchor9 anchor) => anchor >= Anchor9.TopLeft && anchor <= Anchor9.BottomRight;

  public static float AnchorX(Anchor9 anchor) => ValidAnchor(anchor) ? (int)anchor % 3 * .5f : .5f;

  public static float AnchorY(Anchor9 anchor) => ValidAnchor(anchor) ? (int)anchor / 3 * .5f : .5f;

  public static bool ValidGrowth(OverlayGrowth growth) =>
    growth >= OverlayGrowth.TopLeft && growth <= OverlayGrowth.BottomRight;

  /// <summary>The point an element grows from: its own choice, or the anchor's point when Auto.</summary>
  public static Anchor9 GrowPoint(OverlayGrowth growth, Anchor9 anchor) =>
    ValidGrowth(growth) ? (Anchor9)(int)growth
    : ValidAnchor(anchor) ? anchor
    : Anchor9.Center;

  public static Anchor9 SelectAnchor(LayoutBounds bounds, float viewportWidth, float viewportHeight)
  {
    if (!ValidBounds(bounds) || !Positive(viewportWidth) || !Positive(viewportHeight))
      return Anchor9.Center;
    int column = NearestAxis(((double)bounds.X + bounds.Width * .5) / viewportWidth);
    int row = NearestAxis(((double)bounds.Y + bounds.Height * .5) / viewportHeight);
    return (Anchor9)(row * 3 + column);
  }

  // Passing an existing anchor keeps it through a resize. Omitting it chooses
  // the nearest anchor after a move or the first edit of a legacy placement.
  public static AnchoredLayoutPosition Capture(
    LayoutBounds bounds,
    float viewportWidth,
    float viewportHeight,
    Anchor9? anchor = null,
    OverlayGrowth growth = OverlayGrowth.Auto
  )
  {
    if (
      !ValidBounds(bounds)
      || !Positive(viewportWidth)
      || !Positive(viewportHeight)
      || (anchor.HasValue && !ValidAnchor(anchor.Value))
    )
      return new AnchoredLayoutPosition(Anchor9.Center, float.NaN, float.NaN);

    Anchor9 selected = anchor ?? SelectAnchor(bounds, viewportWidth, viewportHeight);
    Anchor9 pivot = GrowPoint(growth, selected);
    double scale = ScreenScale(viewportWidth, viewportHeight);
    float x = (float)(
      (bounds.X + (double)AnchorX(pivot) * bounds.Width - (double)AnchorX(selected) * viewportWidth) / scale
    );
    float y = (float)(
      (bounds.Y + (double)AnchorY(pivot) * bounds.Height - (double)AnchorY(selected) * viewportHeight) / scale
    );
    return new AnchoredLayoutPosition(selected, x, y);
  }

  // Rendering is read-only. Visibility constraints belong to the caller and
  // must never replace the saved offsets when the viewport changes. Callers
  // handle !Customized using their template's default layout before resolving.
  public static LayoutBounds Resolve(
    OverlayLayoutSettings settings,
    float elementWidth,
    float elementHeight,
    float viewportWidth,
    float viewportHeight
  )
  {
    float width = SafeSize(elementWidth);
    float height = SafeSize(elementHeight);
    float screenWidth = SafeSize(viewportWidth);
    float screenHeight = SafeSize(viewportHeight);
    if (IsAnchored(settings))
    {
      float scale = ScreenScale(screenWidth, screenHeight);
      Anchor9 pivot = GrowPoint(settings.GrowFrom, settings.Anchor);
      return new LayoutBounds(
        AnchorX(settings.Anchor) * screenWidth - AnchorX(pivot) * width + settings.OffsetX * scale,
        AnchorY(settings.Anchor) * screenHeight - AnchorY(pivot) * height + settings.OffsetY * scale,
        width,
        height
      );
    }
    return new LayoutBounds(
      LegacyCenter(settings?.CenterX ?? .5f) * screenWidth - width * .5f,
      LegacyCenter(settings?.CenterY ?? .5f) * screenHeight - height * .5f,
      width,
      height
    );
  }

  // The two exact ties favor the center, independently on each normalized axis.
  private static int NearestAxis(double center) =>
    center < .25 ? 0
    : center > .75 ? 2
    : 1;

  private static bool Positive(float value) => OverlayLayoutGeometry.Finite(value) && value > 0;

  private static bool ValidBounds(LayoutBounds bounds) =>
    OverlayLayoutGeometry.Finite(bounds.X)
    && OverlayLayoutGeometry.Finite(bounds.Y)
    && Positive(bounds.Width)
    && Positive(bounds.Height);

  private static float SafeSize(float value) => Positive(value) ? value : 0;

  private static float LegacyCenter(float value) =>
    OverlayLayoutGeometry.Finite(value) ? Math.Max(-4, Math.Min(5, value)) : .5f;
}
