using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Element.Application.Rendering;
using Element.Infrastructure.Ipc;

namespace Element.Infrastructure.Replays;

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

  public void OnReplayBegin(KeyCountRunState recorded, bool render)
  {
    if (render)
      _renderCounts.Counts = recorded?.ByTab;
    else
      _counters.BeginReplay(recorded?.ByTab);
  }

  public void OnReplayEnd()
  {
    _renderCounts.Counts = null;
    _counters.EndReplay();
  }
}

/// <summary>
/// Saved by TUFReplay with each run. Older runs are read back by member name and type, so
/// renaming a member or changing its type needs an <c>UpgradeRunState</c> method on the provider.
/// </summary>
internal sealed class KeyCountRunState
{
  /// <summary>ElementNote tab id → key slot label → presses.</summary>
  public Dictionary<string, Dictionary<string, int>> ByTab = new();
}
