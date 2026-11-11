using System;
using System.Globalization;

namespace Element.Domain.Elements;

/// <summary>A color with straight alpha, each channel 0–1.</summary>
public readonly struct OverlayRgba
{
  public readonly float R,
    G,
    B,
    A;

  public OverlayRgba(float r, float g, float b, float a)
  {
    R = r;
    G = g;
    B = b;
    A = a;
  }

  public OverlayRgba WithAlpha(float a) => new(R, G, B, a);

  /// <summary>"#RRGGBB" or "RRGGBB"; white when it can't be read.</summary>
  public static OverlayRgba Parse(string hex, float alpha = 1)
  {
    if (!string.IsNullOrEmpty(hex))
    {
      string digits = hex[0] == '#' ? hex.Substring(1) : hex;
      if (
        digits.Length == 6
        && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)
      )
        return new OverlayRgba(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, alpha);
    }
    return new OverlayRgba(1, 1, 1, alpha);
  }

  public string ToHex()
  {
    static int Byte(float channel) => Math.Max(0, Math.Min(255, (int)Math.Round(channel * 255)));
    return "#" + Byte(R).ToString("X2") + Byte(G).ToString("X2") + Byte(B).ToString("X2");
  }
}
