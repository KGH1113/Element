namespace Element.Domain.Attempts;

/// <summary>What the overlay shows: attempts from this start and speed, and on the whole map.</summary>
internal readonly struct AttemptCounts
{
  public AttemptCounts(int attempt, int fullAttempt)
  {
    Attempt = attempt;
    FullAttempt = fullAttempt;
  }

  public int Attempt { get; }
  public int FullAttempt { get; }
}
