using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Element.Application.Rendering;
using Element.Infrastructure.ElementNote.Ipc;

namespace Element.Infrastructure.TufReplay;

/// <summary>
/// ElementNote's key counts when a run started, saved with the run by TUFReplay. When TUFReplay
/// replays the run in the game, ElementNote counts the replay's presses from them without adding
/// to the player's totals; when it renders the run, the video's key viewers count from them.
/// TUFReplay calls these public methods by name on the Unity main thread.
/// </summary>
internal sealed class KeyCountRunStateProvider
{
  public const string Id = "Element.KeyCounts";

  // TUFReplay saves the result if it arrives before the run ends.
  private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

  private readonly KeyCounterIpc _counters;
  private readonly RecordedKeyCounts _renderCounts;
  private bool _replaying;
  private bool _rendering;

  public KeyCountRunStateProvider(KeyCounterIpc counters, RecordedKeyCounts renderCounts)
  {
    _counters = counters ?? throw new ArgumentNullException(nameof(counters));
    _renderCounts = renderCounts ?? throw new ArgumentNullException(nameof(renderCounts));
  }

  /// <summary>
  /// Called as a run starts recording, before its first recorded key. ElementNote answers
  /// from its own process, so the counts arrive a moment later; without ElementNote the run
  /// is saved without them.
  /// </summary>
  public Task<KeyCountRunState> CaptureRunState()
  {
    if (!_counters.Available)
      return Task.FromResult<KeyCountRunState>(null);
    return _counters
      .ReadAsync(ReadTimeout)
      .ContinueWith(
        read => read.Result == null ? null : new KeyCountRunState { ByTab = read.Result },
        TaskContinuationOptions.ExecuteSynchronously
      );
  }

  /// <summary>
  /// A replay or render begins. ElementNote hears of an in-game replay together with the run's
  /// counts, in one message, once they arrive in <see cref="OnReplayState"/>: right after this,
  /// before the level starts and before any replayed key.
  /// </summary>
  public void OnReplayBegin(bool render)
  {
    _replaying = true;
    _rendering = render;
    _renderCounts.Counts = null;
  }

  /// <summary>
  /// The counts the replayed run was recorded at, or null for a run recorded without them.
  /// TUFReplay hands them over right after <see cref="OnReplayBegin"/>, and again after a
  /// timeline seek, which starts the run over: ElementNote then counts the replay from them
  /// again instead of adding to them. The keys before the seek's time are not replayed.
  /// </summary>
  public void OnReplayState(KeyCountRunState state, long ticks)
  {
    if (!_replaying)
      return;
    // A render's key viewers read them when the video is first used.
    if (_rendering)
      _renderCounts.Counts = state?.ByTab;
    else
      _counters.BeginReplay(state?.ByTab);
  }

  public void OnReplayEnd()
  {
    _replaying = false;
    _renderCounts.Counts = null;
    _counters.EndReplay();
  }
}
