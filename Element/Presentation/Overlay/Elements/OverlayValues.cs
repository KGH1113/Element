using System.Text;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// The latest text and number of every value. Features write here; elements
/// that use a value redraw only when it changed. Setting an unchanged value
/// costs no allocation and redraws nothing.
/// </summary>
internal sealed class OverlayValues : IOverlayNumberSource
{
  private readonly StringBuilder[] _text = new StringBuilder[OverlayValueCatalog.Count];
  private readonly double[] _number = new double[OverlayValueCatalog.Count];
  private readonly bool[] _has = new bool[OverlayValueCatalog.Count];
  private readonly long[] _stamp = new long[OverlayValueCatalog.Count];
  private ulong _dirty;
  private long _stampNow;

  public OverlayValues()
  {
    for (int i = 0; i < _text.Length; i++)
      _text[i] = new StringBuilder(32);
    WithSamples = new SampleNumbers(this);
  }

  /// <summary>Live numbers, falling back to each value's example for the editor.</summary>
  public IOverlayNumberSource WithSamples { get; }

  /// <summary>
  /// Advances on every change. With <see cref="ChangedSince"/> a second
  /// reader, such as an open editor, sees what changed without taking the
  /// canvas's changes.
  /// </summary>
  public long Stamp => _stampNow;

  /// <summary>The values changed after <paramref name="stamp"/>, as catalog bits.</summary>
  public ulong ChangedSince(long stamp)
  {
    if (stamp >= _stampNow)
      return 0;
    ulong changed = 0;
    for (int i = 0; i < _stamp.Length; i++)
      if (_stamp[i] > stamp)
        changed |= OverlayValueCatalog.Bit((OverlayValueId)i);
    return changed;
  }

  /// <summary>Increments on every combo bump; text elements with the combo pulse once per step.</summary>
  public int ComboBumps { get; private set; }

  /// <summary>A number as shown (<paramref name="value"/>) and as calculated with (<paramref name="number"/>).</summary>
  public void Set(OverlayValueId id, StringBuilder value, double number)
  {
    StringBuilder current = _text[(int)id];
    if (_has[(int)id] && Same(current, value) && _number[(int)id] == number)
      return;
    current.Clear().Append(value);
    _number[(int)id] = number;
    _has[(int)id] = true;
    Changed(id);
  }

  /// <summary>Words such as the song title; they have no number.</summary>
  public void Set(OverlayValueId id, string value)
  {
    StringBuilder current = _text[(int)id];
    if (_has[(int)id] && Same(current, value))
      return;
    current.Clear().Append(value);
    _number[(int)id] = double.NaN;
    _has[(int)id] = true;
    Changed(id);
  }

  public void Clear(OverlayValueId id)
  {
    if (!_has[(int)id])
      return;
    _has[(int)id] = false;
    _text[(int)id].Clear();
    Changed(id);
  }

  private void Changed(OverlayValueId id)
  {
    _dirty |= OverlayValueCatalog.Bit(id);
    _stamp[(int)id] = ++_stampNow;
  }

  public bool TryGetNumber(OverlayValueId id, out double number)
  {
    number = _number[(int)id];
    return _has[(int)id] && !double.IsNaN(number);
  }

  public void BumpCombo() => ComboBumps++;

  public bool TryGet(OverlayValueId id, out StringBuilder value)
  {
    value = _text[(int)id];
    return _has[(int)id];
  }

  /// <summary>The values changed since the last call.</summary>
  public ulong TakeChanges()
  {
    ulong changes = _dirty;
    _dirty = 0;
    return changes;
  }

  /// <summary>An example for the editor while the game has no live value.</summary>
  public static void AppendSample(StringBuilder output, OverlayValueId id)
  {
    OverlayLabels labels = OverlayLocalization.Get(RDString.language);
    switch (id)
    {
      case OverlayValueId.ProgressLabel:
        output.Append(labels.Progress);
        break;
      case OverlayValueId.AccuracyLabel:
        output.Append(labels.XAccuracy);
        break;
      case OverlayValueId.TimeLabel:
        output.Append(labels.MusicTime);
        break;
      default:
        output.Append(OverlayValueCatalog.Get(id).Sample);
        break;
    }
  }

  private sealed class SampleNumbers : IOverlayNumberSource
  {
    private readonly OverlayValues _live;

    public SampleNumbers(OverlayValues live) => _live = live;

    public bool TryGetNumber(OverlayValueId id, out double number)
    {
      if (_live.TryGetNumber(id, out number))
        return true;
      number = OverlayValueCatalog.Get(id).SampleNumber;
      return !double.IsNaN(number);
    }
  }

  private static bool Same(StringBuilder a, StringBuilder b)
  {
    if (a.Length != b.Length)
      return false;
    for (int i = 0; i < a.Length; i++)
      if (a[i] != b[i])
        return false;
    return true;
  }

  private static bool Same(StringBuilder a, string b)
  {
    b ??= string.Empty;
    if (a.Length != b.Length)
      return false;
    for (int i = 0; i < b.Length; i++)
      if (a[i] != b[i])
        return false;
    return true;
  }
}

// Judgment colors values take when they are added to a text.
internal static class JudgementColors
{
  public const string Perfect = "60FF4E";
}
