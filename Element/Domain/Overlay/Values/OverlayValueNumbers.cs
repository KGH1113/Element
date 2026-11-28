using System;
using Element.Domain.Overlay.Variables;

namespace Element.Domain.Overlay.Values;

/// <summary>
/// A value's number at so many decimals. A value keeps its whole number, and
/// each place that reads it takes as many decimals as it chose (by default
/// as many as the overlay shows), brought there the way the game writes that
/// value. Conditions and calculations compare exactly that number, so a
/// progress read to one decimal tells 0.1 from 0. Nothing here allocates.
/// </summary>
internal static class OverlayValueNumbers
{
  /// <summary>The most decimals any value offers.</summary>
  public const int Most = 4;

  private static readonly double[] Scales = { 1, 10, 100, 1000, 10000, 100000 };

  /// <summary>
  /// How many decimals <paramref name="piece"/> reads its value at: what it
  /// chose, or the value's own while it chose nothing; 0 for a value with none.
  /// </summary>
  public static int DigitsOf(OverlayPiece piece, OverlayValueDefinition value) =>
    value.MostDigits <= 0 ? 0
    : piece != null && piece.Digits >= 0 ? Math.Min(piece.Digits, value.MostDigits)
    : value.Digits;

  /// <summary>Whether <paramref name="piece"/> chose its own decimals rather than the value's.</summary>
  public static bool HasOwnDigits(OverlayPiece piece, OverlayValueDefinition value) =>
    value.MostDigits > 0 && piece != null && piece.Digits >= 0 && piece.Digits != value.Digits;

  /// <summary><paramref name="raw"/> at <paramref name="digits"/> decimals, brought there by <paramref name="rounding"/>.</summary>
  public static double Cut(double raw, int digits, OverlayRounding rounding)
  {
    digits = Clamp(digits);
    return Scaled(raw, digits, rounding) / Scales[digits];
  }

  /// <summary>
  /// <paramref name="raw"/> at <paramref name="digits"/> decimals as a whole
  /// number of its last digit (99.52 at 2 is 9952): to write it, or to tell
  /// whether it changed at that many decimals.
  /// </summary>
  public static long Scaled(double raw, int digits, OverlayRounding rounding)
  {
    if (double.IsNaN(raw) || double.IsInfinity(raw))
      return 0;
    digits = Clamp(digits);
    double scale = Scales[digits];
    switch (rounding)
    {
      case OverlayRounding.Floor:
        return (long)Math.Floor(raw * scale);
      case OverlayRounding.Truncate:
      {
        // One digit more, rounded, then dropped: what the game's "##0.00000" less its last digit gives.
        double finer = Math.Round(raw * Scales[digits + 1]);
        long cut = (long)Math.Floor(finer / 10);
        // A percentage that isn't whole yet never reads as 100.
        long whole = (long)(100 * scale);
        return raw < 100 && cut >= whole ? whole - 1 : cut;
      }
      default:
        return (long)Math.Round(raw * scale);
    }
  }

  private static int Clamp(int digits) => Math.Max(0, Math.Min(Most, digits));
}
