using System;
using System.Threading;

namespace ElementKit.Values;

/// <summary>
/// One value your mod shows in Element. Players put it in their texts and
/// calculations like Element's own values; you only call <c>Set</c>.
/// </summary>
/// <remarks>
/// Setting the value it already has does nothing, so calling <c>Set</c> on
/// every hit costs nothing when the value stays the same. <c>Set</c> works
/// from any thread, before <see cref="Kit.Attach"/>, and while Element is
/// turned off; Element shows the latest value on its next frame.
/// </remarks>
public abstract class Value
{
  private readonly object _sync = new();
  private bool _has;
  private double _number = double.NaN;
  private string _text;
  private int _revision;

  private protected Value(ValueGroup group, string key, string koreanName, string englishName)
  {
    Group = group;
    Key = key;
    KoreanName = koreanName;
    EnglishName = englishName;
  }

  /// <summary>The heading this value sits under.</summary>
  public ValueGroup Group { get; }

  /// <summary>The key texts save, without your mod's id: lowercase letters, digits and dashes.</summary>
  public string Key { get; }

  /// <summary>The name players see in Korean.</summary>
  public string KoreanName { get; }

  /// <summary>The name players see in English.</summary>
  public string EnglishName { get; }

  /// <summary>
  /// The key as texts save it, <c>ModId/key</c>; null until <see cref="Kit.Attach"/>
  /// has found the value.
  /// </summary>
  public string FullKey => Group.ModId == null ? null : Group.ModId + "/" + Key;

  /// <summary>Whether the value has data. Without it, texts show nothing where the value goes.</summary>
  public bool HasValue
  {
    get
    {
      lock (_sync)
        return _has;
    }
  }

  /// <summary>Takes the value's data away, for example when a level ends.</summary>
  public void Clear()
  {
    lock (_sync)
    {
      if (!_has)
        return;
      _has = false;
      _number = double.NaN;
      _text = null;
      _revision++;
    }
  }

  internal abstract ValueKind Kind { get; }

  // False when Attach found a problem with the value; it is left out.
  internal bool Accepted { get; set; } = true;

  /// <summary>Advances on every change; reading it needs no lock.</summary>
  internal int Revision => Volatile.Read(ref _revision);

  /// <summary>The value as it is now, read together.</summary>
  internal int Read(out bool has, out double number, out string text)
  {
    lock (_sync)
    {
      has = _has;
      number = _number;
      text = _text;
      return _revision;
    }
  }

  private protected void SetNumber(double number)
  {
    if (double.IsNaN(number) || double.IsInfinity(number))
    {
      Clear();
      return;
    }
    lock (_sync)
    {
      if (_has && _number == number)
        return;
      _has = true;
      _number = number;
      _revision++;
    }
  }

  private protected void SetText(string text)
  {
    if (text == null)
    {
      Clear();
      return;
    }
    lock (_sync)
    {
      if (_has && _text == text)
        return;
      _has = true;
      _text = text;
      _revision++;
    }
  }
}

internal enum ValueKind
{
  Number,
  Time,
  Text,
}

/// <summary>A number players can show and calculate with, such as a key count.</summary>
/// <remarks>Leave units out: players type <c>%</c> or <c>키</c> themselves.</remarks>
public sealed class NumberValue : Value
{
  internal NumberValue(ValueGroup group, string key, string koreanName, string englishName, double sample)
    : base(group, key, koreanName, englishName) => Sample = sample;

  /// <summary>What the editor shows before your mod has set the value.</summary>
  public double Sample { get; }

  internal override ValueKind Kind => ValueKind.Number;

  /// <summary>Shows <paramref name="number"/>. NaN or infinity clears the value.</summary>
  public void Set(double number) => SetNumber(number);
}

/// <summary>A length of time, shown as m:ss and calculated in seconds.</summary>
public sealed class TimeValue : Value
{
  internal TimeValue(ValueGroup group, string key, string koreanName, string englishName, double sampleSeconds)
    : base(group, key, koreanName, englishName) => SampleSeconds = sampleSeconds;

  /// <summary>What the editor shows before your mod has set the value, in seconds.</summary>
  public double SampleSeconds { get; }

  internal override ValueKind Kind => ValueKind.Time;

  /// <summary>Shows <paramref name="seconds"/> as m:ss. NaN or infinity clears the value.</summary>
  public void Set(double seconds) => SetNumber(seconds);

  /// <summary>Shows <paramref name="time"/> as m:ss.</summary>
  public void Set(TimeSpan time) => SetNumber(time.TotalSeconds);
}

/// <summary>Words, such as a pattern name. Players can show them but not calculate with them.</summary>
public sealed class TextValue : Value
{
  internal TextValue(ValueGroup group, string key, string koreanName, string englishName, string sample)
    : base(group, key, koreanName, englishName) => Sample = sample;

  /// <summary>What the editor shows before your mod has set the value.</summary>
  public string Sample { get; }

  internal override ValueKind Kind => ValueKind.Text;

  /// <summary>Shows <paramref name="text"/> as it is; markup isn't read. Null clears the value.</summary>
  public void Set(string text) => SetText(text);
}
