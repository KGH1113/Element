using Element.Domain.Attempts;

namespace Element.Application.Attempts;

/// <summary>
/// The attempts the overlay shows. While TUFReplay replays or renders a run, they are the
/// counts that run was played at, or unknown ("-") for a run recorded without them;
/// otherwise the live counts.
/// </summary>
internal sealed class ReplayedAttempts : IAttemptSource
{
  private readonly IAttemptSource _live;
  private AttemptCounts? _recorded;
  private bool _replaying;

  /// <param name="live">The attempt tracker; null when counting could not start.</param>
  public ReplayedAttempts(IAttemptSource live)
  {
    _live = live;
  }

  public bool Replaying => _replaying;

  public AttemptCounts Read()
  {
    if (_replaying)
      return _recorded ?? AttemptCounts.Unknown;
    return _live?.Read() ?? default;
  }

  /// <summary>A replay begins; its counts are unknown until <see cref="ShowRecorded"/>.</summary>
  public void BeginReplay()
  {
    _replaying = true;
    _recorded = null;
  }

  /// <summary>The replayed run's counts, in place of any shown before. Only during a replay.</summary>
  /// <param name="recorded">The counts the run was played at, or null when it was recorded without them.</param>
  public void ShowRecorded(AttemptCounts? recorded) => _recorded = recorded;

  public void EndReplay()
  {
    _replaying = false;
    _recorded = null;
  }
}
