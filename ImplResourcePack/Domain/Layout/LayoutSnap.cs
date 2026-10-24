using System;
using System.Collections.Generic;

namespace ImplResourcePack.Domain.Layout;

internal static class LayoutSnap
{
  public static LayoutBounds Move(
    LayoutBounds bounds,
    IReadOnlyList<LayoutBounds> others,
    float screenWidth,
    float screenHeight,
    float tolerance,
    out float guideX,
    out float guideY
  ) => Move(bounds, others, screenWidth, screenHeight, tolerance, out guideX, out guideY, out _, out _);

  public static LayoutBounds Move(
    LayoutBounds bounds,
    IReadOnlyList<LayoutBounds> others,
    float screenWidth,
    float screenHeight,
    float tolerance,
    out float guideX,
    out float guideY,
    out LayoutSpacingGuide horizontalSpacing,
    out LayoutSpacingGuide verticalSpacing
  )
  {
    float dx = tolerance + 1,
      dy = tolerance + 1;
    guideX = guideY = float.NaN;
    for (int i = -1; i < others.Count; i++)
    {
      LayoutBounds reference = i < 0 ? new LayoutBounds(0, 0, screenWidth, screenHeight) : others[i];
      for (int a = 0; a < 3; a++)
      for (int b = 0; b < 3; b++)
      {
        float referenceX = reference.X + reference.Width * b * .5f;
        float referenceY = reference.Y + reference.Height * b * .5f;
        float candidateX = referenceX - (bounds.X + bounds.Width * a * .5f);
        float candidateY = referenceY - (bounds.Y + bounds.Height * a * .5f);
        if (
          Math.Abs(candidateX) <= tolerance
          && Math.Abs(candidateX) < Math.Abs(dx)
          && bounds.X + candidateX >= -bounds.Width * (1 - OverlayLayoutGeometry.MinimumVisibleFraction)
          && bounds.X + candidateX <= screenWidth - bounds.Width * OverlayLayoutGeometry.MinimumVisibleFraction
        )
        {
          dx = candidateX;
          guideX = referenceX;
        }
        if (
          Math.Abs(candidateY) <= tolerance
          && Math.Abs(candidateY) < Math.Abs(dy)
          && bounds.Y + candidateY >= -bounds.Height * (1 - OverlayLayoutGeometry.MinimumVisibleFraction)
          && bounds.Y + candidateY <= screenHeight - bounds.Height * OverlayLayoutGeometry.MinimumVisibleFraction
        )
        {
          dy = candidateY;
          guideY = referenceY;
        }
      }
    }
    LayoutBounds aligned = new(
      bounds.X + (float.IsNaN(guideX) ? 0 : dx),
      bounds.Y + (float.IsNaN(guideY) ? 0 : dy),
      bounds.Width,
      bounds.Height
    );
    // Compare against the unsnapped pointer position, so spacing and alignment
    // compete by distance rather than accumulating two corrections.
    float x = LayoutSpacingSnap.Find(
      new LayoutBounds(bounds.X, aligned.Y, bounds.Width, bounds.Height),
      others,
      false,
      screenWidth,
      float.IsNaN(guideX) ? tolerance : Math.Abs(dx),
      out horizontalSpacing
    );
    if (!horizontalSpacing.Visible)
      x = aligned.X;
    float y = LayoutSpacingSnap.Find(
      new LayoutBounds(x, bounds.Y, bounds.Width, bounds.Height),
      others,
      true,
      screenHeight,
      float.IsNaN(guideY) ? tolerance : Math.Abs(dy),
      out verticalSpacing
    );
    if (!verticalSpacing.Visible)
      y = aligned.Y;
    LayoutBounds result = new(x, y, bounds.Width, bounds.Height);
    if (horizontalSpacing.Visible)
    {
      horizontalSpacing = horizontalSpacing.At(result, false);
      if (!horizontalSpacing.Visible)
        result = new LayoutBounds(aligned.X, result.Y, bounds.Width, bounds.Height);
    }
    if (verticalSpacing.Visible)
    {
      verticalSpacing = verticalSpacing.At(result, true);
      if (!verticalSpacing.Visible)
        result = new LayoutBounds(result.X, aligned.Y, bounds.Width, bounds.Height);
    }
    if (horizontalSpacing.Visible)
    {
      horizontalSpacing = horizontalSpacing.At(result, false);
      guideX = float.NaN;
    }
    if (verticalSpacing.Visible)
      guideY = float.NaN;
    return result;
  }
}
