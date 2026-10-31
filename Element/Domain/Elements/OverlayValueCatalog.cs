using System.Collections.Generic;

namespace Element.Domain.Elements;

// Array indexes for live values. Keys, not these numbers, are saved.
internal enum OverlayValueId
{
  Progress,
  Accuracy,
  Time,
  CurrentTime,
  TotalTime,
  Fps,
  TileBpm,
  CurrentBpm,
  KeysPerSecond,
  Judgements,
  Overload,
  TooEarly,
  VeryEarly,
  EarlyPerfect,
  PerfectEarly,
  PurePerfect,
  PerfectLate,
  LatePerfect,
  VeryLate,
  TooLate,
  Miss,
  Combo,
  TimingScale,
  SongTitle,
  Attempt,
  FullAttempt,
  ProgressLabel,
  AccuracyLabel,
  TimeLabel,
  ProgressStart,
  ProgressCurrent,
}

internal enum OverlayValueCategory
{
  Progress,
  Judgement,
  Time,
  Speed,
  Song,
  System,

  // Game-language labels from texts converted before 1.1. They still render,
  // but aren't offered; editing such a text turns them into plain words.
  Legacy,
}

internal sealed class OverlayValueDefinition
{
  public readonly OverlayValueId Id;
  public readonly string Key;
  public readonly OverlayValueCategory Category;
  public readonly string KoreanName;
  public readonly string EnglishName;

  // Shown in the editor while the game has no live value.
  public readonly string Sample;

  public OverlayValueDefinition(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    string koreanName,
    string englishName,
    string sample
  )
  {
    Id = id;
    Key = key;
    Category = category;
    KoreanName = koreanName;
    EnglishName = englishName;
    Sample = sample;
  }
}

internal static class OverlayValueCatalog
{
  public const int Count = (int)OverlayValueId.ProgressCurrent + 1;

  public static readonly OverlayValueCategory[] Categories =
  {
    OverlayValueCategory.Progress,
    OverlayValueCategory.Judgement,
    OverlayValueCategory.Time,
    OverlayValueCategory.Speed,
    OverlayValueCategory.Song,
    OverlayValueCategory.System,
  };

  private static readonly OverlayValueDefinition[] Definitions =
  {
    new(OverlayValueId.Progress, "progress", OverlayValueCategory.Progress, "진행도", "Progress", "0%-48%"),
    new(OverlayValueId.Accuracy, "accuracy", OverlayValueCategory.Judgement, "절대 정확도", "X-Accuracy", "99.5%"),
    new(OverlayValueId.Time, "time", OverlayValueCategory.Time, "시간 (현재~전체)", "Time (now~total)", "1:27~3:48"),
    new(OverlayValueId.CurrentTime, "time-current", OverlayValueCategory.Time, "현재 시간", "Elapsed time", "1:27"),
    new(OverlayValueId.TotalTime, "time-total", OverlayValueCategory.Time, "전체 시간", "Total time", "3:48"),
    new(OverlayValueId.Fps, "fps", OverlayValueCategory.System, "FPS", "FPS", "120"),
    new(OverlayValueId.TileBpm, "tile-bpm", OverlayValueCategory.Speed, "타일 BPM", "Tile BPM", "180"),
    new(OverlayValueId.CurrentBpm, "current-bpm", OverlayValueCategory.Speed, "현재 BPM", "Current BPM", "180"),
    new(OverlayValueId.KeysPerSecond, "kps", OverlayValueCategory.Speed, "초당 입력 수", "Keys per second", "6"),
    new(
      OverlayValueId.Judgements,
      "judgements",
      OverlayValueCategory.Judgement,
      "판정 한 줄",
      "All judgments",
      "0 0 0 2 4 120 4 2 0 0 0"
    ),
    new(OverlayValueId.Overload, "overload", OverlayValueCategory.Judgement, "Overload", "Overload", "0"),
    new(OverlayValueId.TooEarly, "too-early", OverlayValueCategory.Judgement, "Too Early", "Too Early", "0"),
    new(OverlayValueId.VeryEarly, "very-early", OverlayValueCategory.Judgement, "Very Early", "Very Early", "0"),
    new(
      OverlayValueId.EarlyPerfect,
      "early-perfect",
      OverlayValueCategory.Judgement,
      "Early Perfect",
      "Early Perfect",
      "2"
    ),
    new(OverlayValueId.PerfectEarly, "perfect-early", OverlayValueCategory.Judgement, "Perfect −", "Perfect −", "4"),
    new(
      OverlayValueId.PurePerfect,
      "pure-perfect",
      OverlayValueCategory.Judgement,
      "Pure Perfect",
      "Pure Perfect",
      "120"
    ),
    new(OverlayValueId.PerfectLate, "perfect-late", OverlayValueCategory.Judgement, "Perfect +", "Perfect +", "4"),
    new(
      OverlayValueId.LatePerfect,
      "late-perfect",
      OverlayValueCategory.Judgement,
      "Late Perfect",
      "Late Perfect",
      "2"
    ),
    new(OverlayValueId.VeryLate, "very-late", OverlayValueCategory.Judgement, "Very Late", "Very Late", "0"),
    new(OverlayValueId.TooLate, "too-late", OverlayValueCategory.Judgement, "Too Late", "Too Late", "0"),
    new(OverlayValueId.Miss, "miss", OverlayValueCategory.Judgement, "Miss", "Miss", "0"),
    new(OverlayValueId.Combo, "combo", OverlayValueCategory.Judgement, "콤보", "Combo", "120"),
    new(
      OverlayValueId.TimingScale,
      "timing-scale",
      OverlayValueCategory.Judgement,
      "타이밍 배율",
      "Timing scale",
      "100%"
    ),
    new(
      OverlayValueId.SongTitle,
      "song-title",
      OverlayValueCategory.Song,
      "곡 제목",
      "Song title",
      "A Dance of Fire and Ice"
    ),
    new(OverlayValueId.Attempt, "attempt", OverlayValueCategory.Song, "시도 횟수", "Attempts", "3"),
    new(OverlayValueId.FullAttempt, "full-attempt", OverlayValueCategory.Song, "전체 시도 횟수", "Full attempts", "27"),
    new(
      OverlayValueId.ProgressLabel,
      "progress-label",
      OverlayValueCategory.Legacy,
      "'진행도'",
      "'Progress'",
      "진행도"
    ),
    new(
      OverlayValueId.AccuracyLabel,
      "accuracy-label",
      OverlayValueCategory.Legacy,
      "'절대 정확도'",
      "'X-Accuracy'",
      "절대 정확도"
    ),
    new(
      OverlayValueId.TimeLabel,
      "time-label",
      OverlayValueCategory.Legacy,
      "'음악·맵 시간'",
      "'Music / map time'",
      "음악 시간"
    ),
    new(OverlayValueId.ProgressStart, "progress-start", OverlayValueCategory.Progress, "시작 지점", "Start", "0%"),
    new(
      OverlayValueId.ProgressCurrent,
      "progress-current",
      OverlayValueCategory.Progress,
      "현재 진행도",
      "Current",
      "48%"
    ),
  };

  private static readonly Dictionary<string, OverlayValueDefinition> ByKey = CreateIndex();

  public static OverlayValueDefinition Get(OverlayValueId id) => Definitions[(int)id];

  public static IReadOnlyList<OverlayValueDefinition> All => Definitions;

  public static bool TryGet(string key, out OverlayValueDefinition definition)
  {
    definition = null;
    return key != null && ByKey.TryGetValue(key, out definition);
  }

  public static bool IsLegacy(OverlayValueDefinition value) => value.Category == OverlayValueCategory.Legacy;

  public static ulong Bit(OverlayValueId id) => 1UL << (int)id;

  private static Dictionary<string, OverlayValueDefinition> CreateIndex()
  {
    Dictionary<string, OverlayValueDefinition> index = new();
    for (int i = 0; i < Definitions.Length; i++)
    {
      if ((int)Definitions[i].Id != i)
        throw new System.InvalidOperationException("Overlay value " + Definitions[i].Key + " is out of order.");
      index.Add(Definitions[i].Key, Definitions[i]);
    }
    return index;
  }
}
