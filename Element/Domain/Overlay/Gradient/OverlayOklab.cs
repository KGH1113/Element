using System;

namespace Element.Domain.Overlay.Gradient;

/// <summary>
/// Colors mixed the way eyes see them, in Oklab: halfway from white to blue
/// reads halfway, and two bright colors never pass through a muddy gray.
/// Opacities mix along, and a color fading out leaves no trace of its hue.
/// Nothing here allocates.
/// </summary>
internal static class OverlayOklab
{
  /// <summary>The color <paramref name="t"/> (0–1) of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
  public static OverlayRgba Mix(OverlayRgba from, OverlayRgba to, float t)
  {
    if (t <= 0)
      return from;
    if (t >= 1)
      return to;
    float alpha = from.A + (to.A - from.A) * t;
    // Hue mixes by opacity, so a clear stop lends the other none of its color.
    float weightFrom = from.A * (1 - t),
      weightTo = to.A * t,
      weight = weightFrom + weightTo;
    if (weight <= 1e-5f)
    {
      weightFrom = 1 - t;
      weightTo = t;
      weight = 1;
    }
    ToLab(from, out float l1, out float a1, out float b1);
    ToLab(to, out float l2, out float a2, out float b2);
    float l = (l1 * weightFrom + l2 * weightTo) / weight,
      a = (a1 * weightFrom + a2 * weightTo) / weight,
      b = (b1 * weightFrom + b2 * weightTo) / weight;
    return FromLab(l, a, b, alpha);
  }

  /// <summary>Lightness and the two color axes of <paramref name="color"/>, ignoring its opacity.</summary>
  public static void ToLab(OverlayRgba color, out float l, out float a, out float b)
  {
    double r = Linear(color.R),
      g = Linear(color.G),
      bl = Linear(color.B);
    double lms1 = Math.Pow(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * bl, 1.0 / 3),
      lms2 = Math.Pow(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * bl, 1.0 / 3),
      lms3 = Math.Pow(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * bl, 1.0 / 3);
    l = (float)(0.2104542553 * lms1 + 0.7936177850 * lms2 - 0.0040720468 * lms3);
    a = (float)(1.9779984951 * lms1 - 2.4285922050 * lms2 + 0.4505937099 * lms3);
    b = (float)(0.0259040371 * lms1 + 0.7827717662 * lms2 - 0.8086757660 * lms3);
  }

  public static OverlayRgba FromLab(float l, float a, float b, float alpha)
  {
    double lms1 = l + 0.3963377774 * a + 0.2158037573 * b,
      lms2 = l - 0.1055613458 * a - 0.0638541728 * b,
      lms3 = l - 0.0894841775 * a - 1.2914855480 * b;
    lms1 *= lms1 * lms1;
    lms2 *= lms2 * lms2;
    lms3 *= lms3 * lms3;
    double r = 4.0767416621 * lms1 - 3.3077115913 * lms2 + 0.2309699292 * lms3,
      g = -1.2684380046 * lms1 + 2.6097574011 * lms2 - 0.3413193965 * lms3,
      bl = -0.0041960863 * lms1 - 0.7034186147 * lms2 + 1.7076147010 * lms3;
    return new OverlayRgba(Gamma(r), Gamma(g), Gamma(bl), alpha);
  }

  private static double Linear(float channel)
  {
    double c = Math.Max(0, Math.Min(1, channel));
    return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
  }

  private static float Gamma(double linear)
  {
    double c = Math.Max(0, Math.Min(1, linear));
    return (float)(c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055);
  }
}
