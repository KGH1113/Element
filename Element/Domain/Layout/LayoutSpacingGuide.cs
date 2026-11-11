using System;

namespace Element.Domain.Layout;

internal readonly struct LayoutSpacingGuide
{
  public readonly bool Visible;
  public readonly float Start1,
    End1,
    Start2,
    End2,
    Cross;
  private readonly float _crossMin,
    _crossMax;

  public LayoutSpacingGuide(
    float start1,
    float end1,
    float start2,
    float end2,
    float crossMin,
    float crossMax,
    float cross
  )
  {
    Visible = true;
    Start1 = start1;
    End1 = end1;
    Start2 = start2;
    End2 = end2;
    _crossMin = crossMin;
    _crossMax = crossMax;
    Cross = cross;
  }

  public LayoutSpacingGuide At(LayoutBounds bounds, bool vertical)
  {
    float min = Math.Max(_crossMin, vertical ? bounds.X : bounds.Y);
    float max = Math.Min(_crossMax, vertical ? bounds.Right : bounds.Bottom);
    return max > min
      ? new LayoutSpacingGuide(Start1, End1, Start2, End2, _crossMin, _crossMax, (min + max) * .5f)
      : default;
  }
}
