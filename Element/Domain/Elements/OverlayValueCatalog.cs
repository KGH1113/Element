using System.Collections.Generic;

namespace Element.Domain.Elements;

// Array indexes for live values. Keys, not these numbers, are saved.
internal enum OverlayValueId
{
  ProgressStart,
  ProgressCurrent,
  Accuracy,
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
  CurrentTime,
  TotalTime,
  TileBpm,
  CurrentBpm,
  KeysPerSecond,
  SongTitle,
  Attempt,
  FullAttempt,
  Fps,
  ProgressLabel,
  AccuracyLabel,
  TimeLabel,
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

// What a value is to arithmetic: a plain number, a time in seconds shown as
// m:ss, or words that can only be shown as they are.
internal enum OverlayValueKind
{
  Number,
  Time,
  Text,
}

internal sealed class OverlayValueDefinition
{
  public readonly OverlayValueId Id;
  public readonly string Key;
  public readonly OverlayValueCategory Category;
  public readonly OverlayValueKind Kind;
  public readonly string KoreanName;
  public readonly string EnglishName;

  // Shown in the editor while the game has no live value.
  public readonly string Sample;
  public readonly double SampleNumber;

  public OverlayValueDefinition(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    OverlayValueKind kind,
    string koreanName,
    string englishName,
    string sample,
    double sampleNumber
  )
  {
    Id = id;
    Key = key;
    Category = category;
    Kind = kind;
    KoreanName = koreanName;
    EnglishName = englishName;
    Sample = sample;
    SampleNumber = sampleNumber;
  }

  public bool IsNumeric => Kind != OverlayValueKind.Text;
}

internal static class OverlayValueCatalog
{
  public const int Count = (int)OverlayValueId.TimeLabel + 1;

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
    Number(OverlayValueId.ProgressStart, "progress-start", OverlayValueCategory.Progress, "시작 지점", "Start", 0),
    Number(
      OverlayValueId.ProgressCurrent,
      "progress-current",
      OverlayValueCategory.Progress,
      "현재 진행도",
      "Current",
      48
    ),
    Number(OverlayValueId.Accuracy, "accuracy", OverlayValueCategory.Judgement, "절대 정확도", "X-Accuracy", 99.5),
    Judgment(OverlayValueId.Overload, "overload", "Overload", 0),
    Judgment(OverlayValueId.TooEarly, "too-early", "Too Early", 0),
    Judgment(OverlayValueId.VeryEarly, "very-early", "Very Early", 0),
    Judgment(OverlayValueId.EarlyPerfect, "early-perfect", "Early Perfect", 2),
    Judgment(OverlayValueId.PerfectEarly, "perfect-early", "Perfect −", 4),
    Judgment(OverlayValueId.PurePerfect, "pure-perfect", "Pure Perfect", 120),
    Judgment(OverlayValueId.PerfectLate, "perfect-late", "Perfect +", 4),
    Judgment(OverlayValueId.LatePerfect, "late-perfect", "Late Perfect", 2),
    Judgment(OverlayValueId.VeryLate, "very-late", "Very Late", 0),
    Judgment(OverlayValueId.TooLate, "too-late", "Too Late", 0),
    Judgment(OverlayValueId.Miss, "miss", "Miss", 0),
    Number(OverlayValueId.Combo, "combo", OverlayValueCategory.Judgement, "콤보", "Combo", 120),
    Number(
      OverlayValueId.TimingScale,
      "timing-scale",
      OverlayValueCategory.Judgement,
      "타이밍 배율",
      "Timing scale",
      100
    ),
    new(
      OverlayValueId.CurrentTime,
      "time-current",
      OverlayValueCategory.Time,
      OverlayValueKind.Time,
      "현재 시간",
      "Elapsed time",
      "1:27",
      87
    ),
    new(
      OverlayValueId.TotalTime,
      "time-total",
      OverlayValueCategory.Time,
      OverlayValueKind.Time,
      "전체 시간",
      "Total time",
      "3:48",
      228
    ),
    Number(OverlayValueId.TileBpm, "tile-bpm", OverlayValueCategory.Speed, "타일 BPM", "Tile BPM", 180),
    Number(OverlayValueId.CurrentBpm, "current-bpm", OverlayValueCategory.Speed, "현재 BPM", "Current BPM", 180),
    Number(OverlayValueId.KeysPerSecond, "kps", OverlayValueCategory.Speed, "초당 입력 수", "Keys per second", 6),
    Text(
      OverlayValueId.SongTitle,
      "song-title",
      OverlayValueCategory.Song,
      "곡 제목",
      "Song title",
      "A Dance of Fire and Ice"
    ),
    Number(OverlayValueId.Attempt, "attempt", OverlayValueCategory.Song, "시도 횟수", "Attempts", 3),
    Number(
      OverlayValueId.FullAttempt,
      "full-attempt",
      OverlayValueCategory.Song,
      "전체 시도 횟수",
      "Full attempts",
      27
    ),
    Number(OverlayValueId.Fps, "fps", OverlayValueCategory.System, "FPS", "FPS", 120),
    Text(
      OverlayValueId.ProgressLabel,
      "progress-label",
      OverlayValueCategory.Legacy,
      "'진행도'",
      "'Progress'",
      "진행도"
    ),
    Text(
      OverlayValueId.AccuracyLabel,
      "accuracy-label",
      OverlayValueCategory.Legacy,
      "'절대 정확도'",
      "'X-Accuracy'",
      "절대 정확도"
    ),
    Text(
      OverlayValueId.TimeLabel,
      "time-label",
      OverlayValueCategory.Legacy,
      "'음악·맵 시간'",
      "'Music / map time'",
      "음악 시간"
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

  private static OverlayValueDefinition Number(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    string korean,
    string english,
    double sample
  ) => new(id, key, category, OverlayValueKind.Number, korean, english, SampleText(sample), sample);

  // A judgment count: its name is the game's own word in both languages.
  private static OverlayValueDefinition Judgment(OverlayValueId id, string key, string name, double sample) =>
    Number(id, key, OverlayValueCategory.Judgement, name, name, sample);

  private static OverlayValueDefinition Text(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    string korean,
    string english,
    string sample
  ) => new(id, key, category, OverlayValueKind.Text, korean, english, sample, double.NaN);

  private static string SampleText(double sample) => sample.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
