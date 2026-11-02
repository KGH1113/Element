using System;
using System.Collections.Generic;
using Tabub.Planning;
using UnityEngine;

namespace Tabub.Game;

/// <summary>
/// The level being played: planned once when it starts, then on every hit
/// only looked up, so playing allocates nothing.
/// </summary>
internal static class TabubSession
{
  private static readonly TabubSettings Settings = new();
  private static readonly List<double> Beats = new();
  private static readonly List<int> HitFloors = new();
  private static TabubPlan _plan;
  private static int[] _nextHit = Array.Empty<int>();
  private static string _left = "왼손";
  private static string _right = "오른손";

  /// <summary>A level starts or restarts: plan its chart, then show where it stands.</summary>
  public static void Begin()
  {
    Plan();
    Show();
  }

  /// <summary>A hit or a checkpoint moved the planet.</summary>
  public static void Advance() => Show();

  public static void End()
  {
    _plan = null;
    _nextHit = Array.Empty<int>();
    TabubValues.Clear();
  }

  private static void Plan()
  {
    _plan = null;
    List<scrFloor> floors = scrLevelMaker.instance != null ? scrLevelMaker.instance.listFloors : null;
    if (floors == null || floors.Count < 2)
      return;
    LevelBeats.Read(floors, Settings.ReferenceBpm, Beats, HitFloors);
    if (Beats.Count == 0)
      return;
    _plan = TabubPlanner.Plan(Beats, Settings);
    _nextHit = NextHits(floors.Count);
    bool korean = RDString.language == SystemLanguage.Korean;
    _left = korean ? "왼손" : "Left";
    _right = korean ? "오른손" : "Right";
  }

  // For each tile, the first hit after it: what the planet plays next.
  private static int[] NextHits(int floorCount)
  {
    int[] next = new int[floorCount];
    int hit = 0;
    for (int floor = 0; floor < floorCount; floor++)
    {
      while (hit < HitFloors.Count && HitFloors[hit] <= floor)
        hit++;
      next[floor] = hit < HitFloors.Count ? hit : -1;
    }
    return next;
  }

  private static void Show()
  {
    scrController controller = scrController.instance;
    if (_plan == null || controller == null)
    {
      TabubValues.Clear();
      return;
    }
    int floor = controller.currentSeqID;
    int hit = floor >= 0 && floor < _nextHit.Length ? _nextHit[floor] : -1;
    int index = _plan.SectionOf(hit);
    if (index < 0)
    {
      TabubValues.Clear();
      return;
    }
    TabubSection now = _plan.Sections[index];
    TabubValues.Hand.Set(Name(now.Hand));
    TabubValues.Pattern.Set(now.Pattern);
    TabubValues.Keys.Set(now.HitCount);
    TabubSection other = NextOf(index, now.Hand == Hand.Left ? Hand.Right : Hand.Left);
    Show(now.Hand == Hand.Left ? TabubValues.LeftKeys : TabubValues.RightKeys, now);
    Show(now.Hand == Hand.Left ? TabubValues.RightKeys : TabubValues.LeftKeys, other);
    TabubSection next = index + 1 < _plan.Sections.Count ? _plan.Sections[index + 1] : null;
    if (next == null)
    {
      TabubValues.NextHand.Clear();
      TabubValues.NextPattern.Clear();
      return;
    }
    TabubValues.NextHand.Set(Name(next.Hand));
    TabubValues.NextPattern.Set(next.Pattern);
  }

  private static void Show(ElementKit.Values.NumberValue keys, TabubSection section)
  {
    if (section == null)
      keys.Clear();
    else
      keys.Set(section.HitCount);
  }

  private static TabubSection NextOf(int index, Hand hand)
  {
    for (int i = index + 1; i < _plan.Sections.Count; i++)
      if (_plan.Sections[i].Hand == hand)
        return _plan.Sections[i];
    return null;
  }

  private static string Name(Hand hand) => hand == Hand.Left ? _left : _right;
}
