using ImplResourcePack.Application;
using ImplResourcePack.Domain.Judgement;

namespace ImplResourcePack.Infrastructure.Game.Judgement;

internal sealed class JudgementReader : IJudgementSource
{
  public bool TryRead(out JudgementSnapshot snapshot)
  {
    snapshot = default;
    return TryGetSinglePlayerTracker(out scrMarginTracker tracker) && TryRead(tracker, out snapshot);
  }

  public bool TryRead(scrMarginTracker tracker, out JudgementSnapshot snapshot)
  {
    snapshot = default;
    if (
      tracker == null
      || !TryGetSinglePlayerTracker(out scrMarginTracker current)
      || !ReferenceEquals(current, tracker)
    )
      return false;

    int[] hits = tracker.hitMarginsCount;
    snapshot = new JudgementSnapshot(
      Count(hits, HitMargin.FailOverload),
      Count(hits, HitMargin.TooEarly),
      Count(hits, HitMargin.VeryEarly),
      Count(hits, HitMargin.EarlyPerfect),
      Count(hits, HitMargin.PerfectMinus),
      Count(hits, HitMargin.XPerfect) + Count(hits, HitMargin.Auto),
      Count(hits, HitMargin.PerfectPlus),
      Count(hits, HitMargin.LatePerfect),
      Count(hits, HitMargin.VeryLate),
      Count(hits, HitMargin.TooLate),
      Count(hits, HitMargin.FailMiss)
    );
    return true;
  }

  private static bool TryGetSinglePlayerTracker(out scrMarginTracker tracker)
  {
    tracker = null;
    if (scrController.coopMode)
      return false;

    scrMarginTracker[] trackers = scrMistakesManager.marginTrackers;
    if (trackers == null || trackers.Length != 1)
      return false;

    tracker = trackers[0];
    return tracker != null;
  }

  private static int Count(int[] hits, HitMargin margin)
  {
    int index = (int)margin;
    return hits != null && index >= 0 && index < hits.Length ? hits[index] : 0;
  }
}
