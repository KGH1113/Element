namespace Element.Domain.Elements;

/// <summary>A stop as drawn: where it sits (0–1) and its color.</summary>
public readonly struct OverlayGradientSample
{
  public readonly float Position;
  public readonly OverlayRgba Color;

  public OverlayGradientSample(float position, OverlayRgba color)
  {
    Position = position;
    Color = color;
  }
}
