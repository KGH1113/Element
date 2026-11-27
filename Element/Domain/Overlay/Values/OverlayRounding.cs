namespace Element.Domain.Overlay.Values;

/// <summary>
/// How a value with decimals is brought to fewer of them: the way the game
/// itself writes it, so the overlay never shows a number the game wouldn't.
/// </summary>
internal enum OverlayRounding
{
  /// <summary>To the nearest, half to even: values the game doesn't write itself (BPM, KPS, timing scale).</summary>
  Nearest,

  /// <summary>What is past the last digit is dropped, as the game floors progress.</summary>
  Floor,

  /// <summary>
  /// As the game writes X-Accuracy: rounded at one digit more, then that
  /// digit dropped, and never 100 before it is 100.
  /// </summary>
  Truncate,
}
