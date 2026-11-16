using System;
using Element.Application.Attempts;
using Element.Domain.Attempts;

namespace Element.Infrastructure.TufReplay;

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

  /// <summary>
  /// A replay or render begins: the overlay stops showing the live counts. The run's counts
  /// follow in <see cref="OnReplayState"/> before the level starts, and redraw the overlay.
  /// </summary>
  public void OnReplayBegin(bool render) => _replayed.BeginReplay();

  /// <summary>
  /// The counts the replayed run was played at, or null for a run recorded without them.
  /// TUFReplay hands them over right after <see cref="OnReplayBegin"/>, and again after a
  /// timeline seek; each call replaces the counts shown.
  /// </summary>
  public void OnReplayState(AttemptRunState state, long ticks)
  {
    if (!_replayed.Replaying)
      return;
    _replayed.ShowRecorded(state == null ? null : new AttemptCounts(state.Attempt, state.FullAttempt));
    _refresh();
  }

  public void OnReplayEnd()
  {
    _replayed.EndReplay();
    _refresh();
  }
}
