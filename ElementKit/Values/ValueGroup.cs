using System.Collections.Generic;
using System.Threading;

namespace ElementKit.Values;

/// <summary>
/// A heading in Element's value dock and the values under it. Keep groups
/// and their values in <c>static readonly</c> fields; <see cref="Kit.Attach"/>
/// finds them there.
/// </summary>
/// <example>
/// <code>
/// public static class FingeringValues
/// {
///     public static readonly ValueGroup Group = new("타법", "Fingering");
///     public static readonly NumberValue LeftKeys = Group.Number("left-keys", "왼손 키", "Left keys", sample: 3);
/// }
/// </code>
/// </example>
public sealed class ValueGroup
{
  private static int _created;
  private readonly List<Value> _values = new();

  /// <param name="koreanName">The heading in Korean, such as "타법".</param>
  /// <param name="englishName">The heading in English, such as "Fingering".</param>
  public ValueGroup(string koreanName, string englishName)
  {
    KoreanName = koreanName;
    EnglishName = englishName;
    Order = Interlocked.Increment(ref _created);
  }

  /// <summary>The heading in Korean.</summary>
  public string KoreanName { get; }

  /// <summary>The heading in English.</summary>
  public string EnglishName { get; }

  /// <summary>The values in the order they were made.</summary>
  public IReadOnlyList<Value> Values => _values;

  /// <summary>The id of the mod that attached this group; null before <see cref="Kit.Attach"/>.</summary>
  public string ModId { get; internal set; }

  // Groups appear in the dock in the order they were made.
  internal int Order { get; }

  /// <param name="key">What texts save: lowercase letters, digits and dashes, such as "left-keys". Never change it once released.</param>
  /// <param name="koreanName">A short name in Korean, such as "왼손 키".</param>
  /// <param name="englishName">A short name in English, such as "Left keys".</param>
  /// <param name="sample">What the editor shows before your mod sets the value.</param>
  public NumberValue Number(string key, string koreanName, string englishName, double sample) =>
    Add(new NumberValue(this, key, koreanName, englishName, sample));

  /// <param name="key">What texts save: lowercase letters, digits and dashes. Never change it once released.</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="sampleSeconds">What the editor shows before your mod sets the value, in seconds.</param>
  public TimeValue Time(string key, string koreanName, string englishName, double sampleSeconds) =>
    Add(new TimeValue(this, key, koreanName, englishName, sampleSeconds));

  /// <param name="key">What texts save: lowercase letters, digits and dashes. Never change it once released.</param>
  /// <param name="koreanName">A short name in Korean.</param>
  /// <param name="englishName">A short name in English.</param>
  /// <param name="sample">What the editor shows before your mod sets the value.</param>
  public TextValue Text(string key, string koreanName, string englishName, string sample) =>
    Add(new TextValue(this, key, koreanName, englishName, sample));

  private T Add<T>(T value)
    where T : Value
  {
    lock (_values)
      _values.Add(value);
    return value;
  }
}
