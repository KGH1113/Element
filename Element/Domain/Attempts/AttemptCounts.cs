namespace Element.Domain.Attempts;

/// <summary>What the overlay shows: attempts from this start and speed, and on the whole map.</summary>
internal readonly struct AttemptCounts
{
  public AttemptCounts(int attempt, int fullAttempt)
  {
    Attempt = attempt;
    FullAttempt = fullAttempt;
    IsUnknown = false;
  }

  private AttemptCounts(bool unknown)
  {
    Attempt = 0;
    FullAttempt = 0;
    IsUnknown = unknown;
  }

  /// <summary>
  /// The counts of a replayed run that was recorded without them, such as a run recorded
  /// before TUFReplay saved Element's state. The overlay shows "-".
  /// </summary>
  public static AttemptCounts Unknown => new(true);

  public int Attempt { get; }
  public int FullAttempt { get; }
  public bool IsUnknown { get; }
}
