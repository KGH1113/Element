using System;
using System.Collections.Generic;
using System.Text;
using Tabub.Planning;

namespace Tabub.Game;

/// <summary>
/// The level being played. Its groups are counted once when play starts;
/// each frame then only checks whether the planet reached another tile, and
/// the text is written again only when a group is done.
/// </summary>
internal static class TabubSession
{
  // Four lines of pairs, like the played text.
  private const int AheadGroups = 32;

  private static readonly List<TabubStep> Steps = new();
  private static readonly StringBuilder Builder = new(256);
  private static TabubChart _chart;
  private static int[] _nextGroup = Array.Empty<int>();
  private static int _start;
  private static int _floor;
  private static int _shown = -1;

  /// <summary>Play starts on <paramref name="startFloor"/>: count from the tile after it, as the tag does.</summary>
  public static void Begin(int startFloor)
  {
    _chart = null;
    _shown = -1;
    List<scrFloor> floors = scrLevelMaker.instance != null ? scrLevelMaker.instance.listFloors : null;
    scrConductor conductor = scrConductor.instance;
    if (floors == null || conductor == null || startFloor < 0 || startFloor >= floors.Count - 1)
    {
      TabubValues.Clear();
      return;
    }
    LevelSteps.Read(floors, startFloor, conductor.bpm, Steps);
    _chart = TabubChart.Count(Steps);
    _start = startFloor;
    _floor = startFloor;
    _nextGroup = NextGroups(floors.Count - startFloor);
    Show();
  }

  /// <summary>Once a frame: one comparison unless the planet reached another tile.</summary>
  public static void Tick()
  {
    if (_chart == null)
      return;
    scrController controller = scrController.instance;
    if (controller == null)
      return;
    int floor = controller.currentSeqID;
    if (floor == _floor)
      return;
    if (floor < _floor)
    {
      // Back to a checkpoint: count again from there, like a restart.
      Begin(floor);
      return;
    }
    _floor = floor;
    Show();
  }

  public static void End()
  {
    _chart = null;
    _nextGroup = Array.Empty<int>();
    _shown = -1;
    TabubValues.Clear();
  }

  // For each tile from the start, the group of the next hit after it; the
  // group count once no hit is left.
  private static int[] NextGroups(int length)
  {
    int[] nextGroup = new int[length];
    int next = _chart.GroupCount;
    for (int offset = length - 1; offset >= 0; offset--)
    {
      // The tile after start + offset is step offset.
      int group = _chart.GroupOf(offset);
      if (group >= 0)
        next = group;
      nextGroup[offset] = next;
    }
    return nextGroup;
  }

  private static void Show()
  {
    int offset = _floor - _start;
    if (_chart == null || offset < 0 || offset >= _nextGroup.Length)
    {
      TabubValues.Clear();
      return;
    }
    // Every group before the next hit's is done.
    int group = _nextGroup[offset];
    if (group == _shown)
      return;
    _shown = group;
    IReadOnlyList<int> counts = _chart.Counts;
    TabubValues.Played.Set(TabubText.Played(counts, group, Builder));
    if (group >= counts.Count)
    {
      TabubValues.Ahead.Clear();
      TabubValues.Pair.Clear();
      TabubValues.NextPair.Clear();
      TabubValues.Keys.Clear();
      return;
    }
    TabubValues.Ahead.Set(TabubText.Ahead(counts, group, AheadGroups, Builder));
    TabubValues.Pair.Set(TabubText.Pair(counts, group));
    TabubValues.NextPair.Set(TabubText.Pair(counts, group - group % 2 + 2));
    TabubValues.Keys.Set(counts[group]);
  }
}
