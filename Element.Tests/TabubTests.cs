using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Tabub.Planning;

namespace Element.Tests;

// Tabub counts keys the way the TabubuLabubu tag does: groups of 180° doubled
// until the BPM is under 1050, written in pairs, four pairs a line, the latest
// 34 groups at most.
internal static class TabubTests
{
  public static void Run()
  {
    GroupsGrowWithTheBpm();
    KeysAreCountedPerGroup();
    TextIsWrittenLikeTheTag();
    PlayedTextMatchesTheTagHitByHit();
  }

  private static void GroupsGrowWithTheBpm()
  {
    Check(TabubUnits.Threshold(1000) == 1 && TabubUnits.Threshold(1049.9) == 1, "under 1050 BPM a group is half a turn");
    Check(TabubUnits.Threshold(1050) == 2 && TabubUnits.Threshold(2021) == 2, "1050 BPM and up: a full turn");
    Check(TabubUnits.Threshold(2100) == 4 && TabubUnits.Threshold(4000) == 4, "and twice again past 2100");
    Check(Near(TabubUnits.FromTurn(180, 1000), 1) && Near(TabubUnits.FromTurn(360, 2021), 1), "180° at 1000 and 360° at 2021 are one group");
    Check(Near(TabubUnits.FromSeconds(60 / 1010.5, 2021), 1), "a tile's time gives the same groups as its turn");
  }

  private static void KeysAreCountedPerGroup()
  {
    TabubChart stream = Chart(1, .5, 1, .5, 1, .5, 1, .5, 1, .5, 1, .5);
    Check(stream.Counts.SequenceEqual(new[] { 2, 2, 2 }), "two keys a group in a stream of quarter turns: " + Show(stream));

    TabubChart rest = Chart(1, .5, 1, 2.5, 1, .5, 1, .5);
    Check(rest.Counts.SequenceEqual(new[] { 2, 0, 0, 2 }), "a rest counts 0 keys: " + Show(rest));

    TabubChart thirds = Chart(1, 1 / 3.0, 1, 1 / 3.0, 1, 1 / 3.0, 1, 0);
    Check(thirds.Counts.SequenceEqual(new[] { 3, 1 }) && thirds.GroupOf(3) == 1, "a hit on a boundary, even a hair early, starts the next group");

    TabubChart midspin = Chart(1, .5, 0, .5, 1, .5);
    Check(midspin.Counts.SequenceEqual(new[] { 1, 1 }) && midspin.GroupOf(1) == -1, "a midspin is passed without a key, but its gap counts");

    TabubChart taps = Chart(2, 1, 1, 0);
    Check(taps.Counts.SequenceEqual(new[] { 2, 1 }), "a tile that needs two taps takes two keys");
  }

  private static void TextIsWrittenLikeTheTag()
  {
    StringBuilder builder = new();
    int[] counts = { 4, 4, 3, 2, 10, 8, 1, 1, 2, 2 };
    Check(
      TabubText.Played(counts, 10, builder) == "44 32 [10 8] 11 \n",
      "pairs, [a b] past 8, a line after four pairs, the last pair once the next group is done"
    );
    Check(TabubText.Played(counts, 0, builder) == "", "nothing before the first group is done");

    int[] long_ = Enumerable.Range(0, 50).Select(i => i % 9).ToArray();
    string from8 = TabubText.Played(long_, 35, builder);
    Check(from8.StartsWith("" + long_[8] + long_[9] + " "), "past 34 groups the oldest eight go");
    Check(TabubText.Played(long_, 42, builder).StartsWith("" + long_[8] + long_[9]), "until it holds 34 again");
    Check(TabubText.Played(long_, 43, builder).StartsWith("" + long_[16] + long_[17]), "then eight more go");

    int[] ahead = { 4, 4, 3, 2, 1, 1, 2, 2, 3, 3 };
    Check(TabubText.Ahead(ahead, 1, 32, builder) == "44 32 11 22 \n33", "what comes, four pairs a line: " + TabubText.Ahead(ahead, 1, 32, builder));
    Check(TabubText.Ahead(ahead, 2, 4, builder) == "32 11", "from the pair the next hit is in");
    Check(TabubText.Pair(ahead, 3) == "32" && TabubText.Pair(new[] { 9, 4 }, 0) == "[9 4]", "the pair now");
    Check(TabubText.Pair(ahead, 10) == null && TabubText.Pair(new[] { 5 }, 0) == "50", "nothing past the end; a lone group pairs with 0");
  }

  // The tag itself, at a frame rate that sees every tile: on each tile it adds
  // the turn to the next tile in whole degrees and counts a key; whenever the
  // turn reaches 180° × threshold it writes down the keys so far.
  private sealed class Tag
  {
    private readonly List<int> _keys = new();
    private int _storage;
    private int _current;

    public void Tile(int degreesToNext, double tileBpm)
    {
      int threshold = TabubUnits.Threshold(tileBpm);
      _storage += degreesToNext;
      _current++;
      // The tag checks every frame, so a long gap writes its empty groups one frame after another.
      while (_storage >= 180 * threshold)
      {
        _storage -= 180 * threshold;
        _keys.Add(_current);
        _current = 0;
        if (_keys.Count > 34)
          _keys.RemoveRange(0, 8);
      }
    }

    public string Text()
    {
      StringBuilder text = new();
      for (int i = 0; i < Math.Min(_keys.Count, 34); i++)
      {
        if (i % 2 == 0 && i > 0)
        {
          int a = _keys[i - 2],
            b = _keys[i - 1];
          text.Append(a > 8 || b > 8 ? "[" + a + " " + b + "] " : "" + a + b + " ");
        }
        if (i % 8 == 0 && i > 0)
          text.Append('\n');
      }
      return text.ToString();
    }
  }

  private static void PlayedTextMatchesTheTagHitByHit()
  {
    int[] pattern = { 90, 90, 180, 45, 45, 90, 60, 60, 60, 30, 30, 120, 180, 360, 90, 90, 540, 45, 45, 45, 45, 30, 30, 30, 90 };
    List<(int Degrees, double Bpm)> tiles = new();
    for (int round = 0; round < 6; round++)
      tiles.AddRange(pattern.Select(degrees => (degrees, 1000.0)));
    // The speed changes on a group boundary, where the tag's turn is back at 0.
    int sum = tiles.Sum(tile => tile.Degrees);
    if (sum % 180 != 0)
      tiles.Add((180 - sum % 180, 1000.0));
    for (int round = 0; round < 6; round++)
      tiles.AddRange(pattern.Select(degrees => (degrees, 2021.0)));

    TabubChart chart = TabubChart.Count(tiles.Select(tile => new TabubStep(1, TabubUnits.FromTurn(tile.Degrees, tile.Bpm))).ToList());
    Tag tag = new();
    StringBuilder builder = new();
    for (int hit = 0; hit < tiles.Count - 1; hit++)
    {
      tag.Tile(tiles[hit].Degrees, tiles[hit].Bpm);
      string expected = tag.Text();
      // After a hit, every group before the next hit's is done.
      string actual = TabubText.Played(chart.Counts, chart.GroupOf(hit + 1), builder);
      Check(expected == actual, "after hit " + hit + " the tag shows\n" + expected + "\nbut Tabub shows\n" + actual);
    }
  }

  // Pairs of (keys, groups to the next tile).
  private static TabubChart Chart(params double[] pairs)
  {
    List<TabubStep> steps = new();
    for (int i = 0; i + 1 < pairs.Length; i += 2)
      steps.Add(new TabubStep((int)pairs[i], pairs[i + 1]));
    return TabubChart.Count(steps);
  }

  private static string Show(TabubChart chart) => string.Join(",", chart.Counts);

  private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
