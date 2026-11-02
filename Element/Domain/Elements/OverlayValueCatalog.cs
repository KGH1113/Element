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

  // Values another mod adds through ElementKit. The dock shows them under
  // their group's own name.
  Extension,
}

// What a value is to arithmetic: a plain number, a time in seconds shown as
// m:ss, or words that can only be shown as they are.
internal enum OverlayValueKind
{
  Number,
  Time,
  Text,
}

// Where another mod's values sit in the dock: a heading in both languages,
// and the mod that added them.
internal sealed class OverlayValueGroup
{
  public readonly string Source;
  public readonly string KoreanName;
  public readonly string EnglishName;

  public OverlayValueGroup(string source, string koreanName, string englishName)
  {
    Source = source ?? string.Empty;
    KoreanName = koreanName ?? string.Empty;
    EnglishName = englishName ?? string.Empty;
  }
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

  // Another mod's heading; null for the built-in values.
  public readonly OverlayValueGroup Group;

  // False while the mod that adds the value is missing or turned off: texts
  // keep it, the overlay shows nothing there and the dock doesn't offer it.
  public readonly bool Available;

  public OverlayValueDefinition(
    OverlayValueId id,
    string key,
    OverlayValueCategory category,
    OverlayValueKind kind,
    string koreanName,
    string englishName,
    string sample,
    double sampleNumber,
    OverlayValueGroup group = null,
    bool available = true
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
    Group = group;
    Available = available;
  }

  public bool IsNumeric => Kind != OverlayValueKind.Text;

  public bool IsExtension => Category == OverlayValueCategory.Extension;
}

// One heading of the dock and the values under it.
internal readonly struct OverlayValueSection
{
  public readonly OverlayValueCategory Category;
  public readonly OverlayValueGroup Group;
  public readonly IReadOnlyList<OverlayValueDefinition> Values;

  public OverlayValueSection(OverlayValueCategory category, OverlayValueGroup group, IReadOnlyList<OverlayValueDefinition> values)
  {
    Category = category;
    Group = group;
    Values = values;
  }
}

// Every value a text or a shape can use. The built-in values keep fixed ids;
// values other mods add, and keys saved by mods that aren't loaded, take the
// ids after them for the rest of the session. Changed on the main thread only.
internal static class OverlayValueCatalog
{
  public const int BuiltInCount = (int)OverlayValueId.TimeLabel + 1;

  /// <summary>Built-in and added values together; one value mask holds them all.</summary>
  public const int Capacity = OverlayValueMask.Capacity;

  public static readonly OverlayValueCategory[] Categories =
  {
    OverlayValueCategory.Progress,
    OverlayValueCategory.Judgement,
    OverlayValueCategory.Time,
    OverlayValueCategory.Speed,
    OverlayValueCategory.Song,
    OverlayValueCategory.System,
  };

  private static readonly OverlayValueDefinition[] BuiltInDefinitions =
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

  private static readonly List<OverlayValueDefinition> Definitions = new(BuiltInDefinitions);
  private static readonly Dictionary<string, OverlayValueDefinition> ByKey = CreateIndex();

  public static int Count => Definitions.Count;

  /// <summary>Advances whenever a value is added, withdrawn or comes back, so the dock can rebuild.</summary>
  public static int Version { get; private set; }

  public static OverlayValueDefinition Get(OverlayValueId id) => Definitions[(int)id];

  public static IReadOnlyList<OverlayValueDefinition> All => Definitions;

  public static IReadOnlyList<OverlayValueDefinition> BuiltIn => BuiltInDefinitions;

  public static bool TryGet(string key, out OverlayValueDefinition definition)
  {
    definition = null;
    return key != null && ByKey.TryGetValue(key, out definition);
  }

  /// <summary>
  /// Like <see cref="TryGet"/>, but a mod's key (<c>Mod/key</c>) nobody has
  /// added yet gets a place, so a text keeps it while that mod is missing.
  /// False for other unknown keys, such as a built-in value that was removed.
  /// </summary>
  public static bool TryResolve(string key, out OverlayValueDefinition definition)
  {
    if (TryGet(key, out definition))
      return true;
    if (!IsExtensionKey(key) || Definitions.Count >= Capacity)
      return false;
    definition = Missing(NextId(), key, null, null, null);
    Add(definition);
    return true;
  }

  /// <summary>
  /// Adds another mod's value, or brings back one with the same key: a text
  /// already using the key starts showing it. Null when the key isn't a
  /// mod's key or the catalog is full.
  /// </summary>
  public static OverlayValueDefinition Register(
    string key,
    OverlayValueGroup group,
    OverlayValueKind kind,
    string koreanName,
    string englishName,
    double sampleNumber,
    string sampleText
  )
  {
    if (!IsExtensionKey(key))
      return null;
    OverlayValueId id;
    if (ByKey.TryGetValue(key, out OverlayValueDefinition existing))
      id = existing.Id;
    else if (Definitions.Count < Capacity)
      id = NextId();
    else
      return null;
    string sample = kind switch
    {
      OverlayValueKind.Text => sampleText ?? string.Empty,
      OverlayValueKind.Time => ClockText(sampleNumber),
      _ => SampleText(sampleNumber),
    };
    OverlayValueDefinition definition = new(
      id,
      key,
      OverlayValueCategory.Extension,
      kind,
      koreanName,
      englishName,
      sample,
      kind == OverlayValueKind.Text ? double.NaN : sampleNumber,
      group
    );
    if (existing != null)
      Replace(definition);
    else
      Add(definition);
    return definition;
  }

  /// <summary>
  /// The mod that added <paramref name="key"/> went away or was turned off.
  /// Its id and name stay so texts keep it; it comes back on the next
  /// <see cref="Register"/>.
  /// </summary>
  public static void Withdraw(string key)
  {
    if (!TryGet(key, out OverlayValueDefinition value) || !value.IsExtension || !value.Available)
      return;
    Replace(Missing(value.Id, key, value.Group, value.KoreanName, value.EnglishName, value.Kind));
  }

  /// <summary>
  /// The dock's headings in order: the built-in categories, then each added
  /// group in the order it arrived. Only values that can be added are listed.
  /// </summary>
  public static List<OverlayValueSection> Sections()
  {
    List<OverlayValueSection> sections = new();
    foreach (OverlayValueCategory category in Categories)
    {
      List<OverlayValueDefinition> values = new();
      foreach (OverlayValueDefinition value in BuiltInDefinitions)
        if (value.Category == category)
          values.Add(value);
      sections.Add(new OverlayValueSection(category, null, values));
    }
    List<OverlayValueGroup> groups = new();
    List<List<OverlayValueDefinition>> grouped = new();
    for (int i = BuiltInCount; i < Definitions.Count; i++)
    {
      OverlayValueDefinition value = Definitions[i];
      if (!value.Available || value.Group == null)
        continue;
      int index = groups.IndexOf(value.Group);
      if (index < 0)
      {
        groups.Add(value.Group);
        grouped.Add(new List<OverlayValueDefinition>());
        index = groups.Count - 1;
      }
      grouped[index].Add(value);
    }
    for (int i = 0; i < groups.Count; i++)
      sections.Add(new OverlayValueSection(OverlayValueCategory.Extension, groups[i], grouped[i]));
    return sections;
  }

  /// <summary>
  /// A mod's key: the mod's id, a slash, then lowercase letters, digits and
  /// dashes, such as <c>Fingering/left-keys</c>. Both parts start with a
  /// letter, so a calculation such as <c>10-4/2</c> is never taken for one.
  /// </summary>
  public static bool IsExtensionKey(string key)
  {
    if (string.IsNullOrEmpty(key) || key.Length > 96)
      return false;
    int slash = key.IndexOf('/');
    if (slash <= 0 || slash == key.Length - 1 || key.IndexOf('/', slash + 1) >= 0)
      return false;
    if (!IsAsciiLetter(key[0]) || !(key[slash + 1] >= 'a' && key[slash + 1] <= 'z'))
      return false;
    for (int i = 0; i < slash; i++)
    {
      char c = key[i];
      if (!(char.IsLetterOrDigit(c) && c < 128) && c != '_' && c != '-' && c != '.')
        return false;
    }
    for (int i = slash + 1; i < key.Length; i++)
    {
      char c = key[i];
      if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
        return false;
    }
    return true;
  }

  private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

  public static bool IsLegacy(OverlayValueDefinition value) => value.Category == OverlayValueCategory.Legacy;

  public static OverlayValueMask Bit(OverlayValueId id) => OverlayValueMask.Of(id);

  /// <summary>Forgets every added value; for tests that need the built-in catalog alone.</summary>
  internal static void ForgetExtensions()
  {
    for (int i = Definitions.Count - 1; i >= BuiltInCount; i--)
    {
      ByKey.Remove(Definitions[i].Key);
      Definitions.RemoveAt(i);
    }
    Version++;
  }

  private static OverlayValueId NextId() => (OverlayValueId)Definitions.Count;

  private static void Add(OverlayValueDefinition definition)
  {
    Definitions.Add(definition);
    ByKey[definition.Key] = definition;
    Version++;
  }

  private static void Replace(OverlayValueDefinition definition)
  {
    Definitions[(int)definition.Id] = definition;
    ByKey[definition.Key] = definition;
    Version++;
  }

  // Calculations keep a missing value as a number, so a formula using it
  // stays whole until its mod is back. One nobody added this session is
  // named by its key, which says which mod it needs.
  private static OverlayValueDefinition Missing(
    OverlayValueId id,
    string key,
    OverlayValueGroup group,
    string koreanName,
    string englishName,
    OverlayValueKind kind = OverlayValueKind.Number
  ) =>
    new(
      id,
      key,
      OverlayValueCategory.Extension,
      kind,
      koreanName ?? key,
      englishName ?? key,
      string.Empty,
      double.NaN,
      group,
      false
    );

  private static string ClockText(double seconds)
  {
    if (double.IsNaN(seconds) || double.IsInfinity(seconds))
      return "0:00";
    long whole = (long)System.Math.Round(System.Math.Abs(seconds));
    return (seconds < 0 ? "-" : "") + (whole / 60) + ":" + (whole % 60).ToString("00");
  }

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
    for (int i = 0; i < BuiltInDefinitions.Length; i++)
    {
      if ((int)BuiltInDefinitions[i].Id != i)
        throw new System.InvalidOperationException("Overlay value " + BuiltInDefinitions[i].Key + " is out of order.");
      index.Add(BuiltInDefinitions[i].Key, BuiltInDefinitions[i]);
    }
    return index;
  }
}
