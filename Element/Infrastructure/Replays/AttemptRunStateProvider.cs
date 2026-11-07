using System;
using Element.Application;
using Element.Application.Attempts;
using Element.Domain.Attempts;

namespace Element.Infrastructure.Replays;

/// <summary>
/// The attempt counts a run was played at, saved with the run by TUFReplay and shown again
/// when TUFReplay replays or renders it. TUFReplay calls these public methods by name on
/// the Unity main thread.
/// </summary>
internal sealed class AttemptRunStateProvider
{
  public const string Id = "Element.Attempts";

  private readonly IAttemptSource _live;
  private readonly ReplayedAttempts _replayed;
  private readonly Action _refresh;

  /// <param name="live">The attempt tracker; null when counting could not start.</param>
  /// <param name="refresh">Redraws the overlay if it is shown.</param>
  public AttemptRunStateProvider(IAttemptSource live, ReplayedAttempts replayed, Action refresh)
  {
    _live = live;
    _replayed = replayed ?? throw new ArgumentNullException(nameof(replayed));
    _refresh = refresh ?? (() => { });
  }

  /// <summary>
  /// Called as a run starts recording. The game already started the play, so the tracker
  /// holds this play's counts, the ones the overlay shows.
  /// </summary>
  public AttemptRunState CaptureRunState()
  {
    if (_live == null)
      return null;
    AttemptCounts counts = _live.Read();
    return new AttemptRunState { Attempt = counts.Attempt, FullAttempt = counts.FullAttempt };
  }

  public void OnReplayBegin(AttemptRunState recorded, bool render)
  {
    _replayed.BeginReplay(recorded == null ? null : new AttemptCounts(recorded.Attempt, recorded.FullAttempt));
    _refresh();
  }

  public void OnReplayEnd()
  {
    _replayed.EndReplay();
    _refresh();
  }
}

/// <summary>
/// Saved by TUFReplay with each run. Older runs are read back by member name and type, so
/// renaming a member or changing its type needs an <c>UpgradeRunState</c> method on the provider.
/// </summary>
internal sealed class AttemptRunState
{
  public int Attempt;
  public int FullAttempt;
}
