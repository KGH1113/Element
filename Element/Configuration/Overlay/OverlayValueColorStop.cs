using System;

namespace Element.Configuration.Overlay;

// One stop of a color that followed a value (OverlayValueColor, from earlier
// versions): at this value, this color. It follows the highlight color,
// keeping only its own opacity, or is a fixed custom color, like every
// overlay color. Hex is unused while it follows the highlight.
[Serializable]
[CopiedInPlace]
public sealed class OverlayValueColorStop
{
  public float At;
  public bool Highlight;
  public string Hex = "#FFFFFF";
  public float Alpha = 1f;

  public static OverlayValueColorStop Custom(float at, string hex, float alpha = 1f) =>
    new()
    {
      At = at,
      Hex = hex,
      Alpha = alpha,
    };

  public static OverlayValueColorStop HighlightColor(float at, float alpha = 1f) =>
    new()
    {
      At = at,
      Highlight = true,
      Alpha = alpha,
    };

  public OverlayValueColorStop Clone() =>
    new()
    {
      At = At,
      Highlight = Highlight,
      Hex = Hex,
      Alpha = Alpha,
    };

  public bool SameAs(OverlayValueColorStop other) =>
    other != null
    && At == other.At
    && Highlight == other.Highlight
    && Alpha == other.Alpha
    && (Highlight || Hex == other.Hex);
}
