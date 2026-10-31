using System.Text;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// The latest text of every value. Features write here; text elements that use
/// a value redraw only when it changed. Setting an unchanged value costs no
/// allocation and redraws nothing.
/// </summary>
internal sealed class OverlayValues
{
  private readonly StringBuilder[] _text = new StringBuilder[OverlayValueCatalog.Count];
  private readonly bool[] _has = new bool[OverlayValueCatalog.Count];
  private ulong _dirty;

  public OverlayValues()
  {
    for (int i = 0; i < _text.Length; i++)
      _text[i] = new StringBuilder(32);
  }

  /// <summary>Increments on every combo bump; text elements with the combo pulse once per step.</summary>
  public int ComboBumps { get; private set; }

  public void Set(OverlayValueId id, StringBuilder value)
  {
    StringBuilder current = _text[(int)id];
    if (_has[(int)id] && Same(current, value))
      return;
    current.Clear().Append(value);
    _has[(int)id] = true;
    _dirty |= OverlayValueCatalog.Bit(id);
  }

  public void Set(OverlayValueId id, string value)
  {
    StringBuilder current = _text[(int)id];
    if (_has[(int)id] && Same(current, value))
      return;
    current.Clear().Append(value);
    _has[(int)id] = true;
    _dirty |= OverlayValueCatalog.Bit(id);
  }

  public void Clear(OverlayValueId id)
  {
    if (!_has[(int)id])
      return;
    _has[(int)id] = false;
    _text[(int)id].Clear();
    _dirty |= OverlayValueCatalog.Bit(id);
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
      case OverlayValueId.Judgements:
        JudgementColors.AppendRow(output, 0, 0, 0, 2, 4, 120, 4, 2, 0, 0, 0);
        break;
      default:
        output.Append(OverlayValueCatalog.Get(id).Sample);
        break;
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

// The judgment row's own colors, shared by the live value and its sample.
internal static class JudgementColors
{
  public const string Perfect = "60FF4E";

  public static StringBuilder AppendRow(
    StringBuilder builder,
    int overload,
    int tooEarly,
    int veryEarly,
    int earlyPerfect,
    int perfectEarly,
    int purePerfect,
    int perfectLate,
    int latePerfect,
    int veryLate,
    int tooLate,
    int miss
  )
  {
    builder
      .AppendInt(overload)
      .Append(" <color=#FF0000>")
      .AppendInt(tooEarly)
      .Append("</color> <color=#FF6F4E>")
      .AppendInt(veryEarly)
      .Append("</color> <color=#A0FF4E>")
      .AppendInt(earlyPerfect)
      .Append("</color> <color=#")
      .Append(Perfect)
      .Append('>')
      .AppendInt(perfectEarly)
      .Append("</color> <color=#");
    AppendPurePerfect(builder)
      .Append('>')
      .AppendInt(purePerfect)
      .Append("</color> <color=#")
      .Append(Perfect)
      .Append('>')
      .AppendInt(perfectLate)
      .Append("</color> <color=#A0FF4E>")
      .AppendInt(latePerfect)
      .Append("</color> <color=#FF6F4E>")
      .AppendInt(veryLate)
      .Append("</color> <color=#FF0000>")
      .AppendInt(tooLate)
      .Append("</color> ")
      .AppendInt(miss);
    return builder;
  }

  private static StringBuilder AppendPurePerfect(StringBuilder builder)
  {
    ColourSchemeHitMargin colors = RDC.data != null ? RDC.hitMarginColoursBySettings : null;
    return colors != null ? builder.AppendHexRgb(colors.colourXPerfect) : builder.Append(Perfect);
  }
}
