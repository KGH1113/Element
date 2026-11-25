using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Conditions;

/// <summary>
/// A condition ready to check: both sides parsed once, so checking it while
/// values change allocates nothing. Numbers the same to six decimals count as
/// the same, so 0.1 + 0.2 is 0.3 here.
/// </summary>
internal sealed class OverlayConditionCheck
{
  private readonly OverlayFormula _value;
  private readonly OverlayFormula _than;
  private readonly bool _smaller;
  private readonly bool _same;
  private readonly bool _bigger;

  public OverlayConditionCheck(OverlayCondition condition)
  {
    _value = OverlayFormula.Parse(condition?.Value);
    _than = OverlayFormula.Parse(condition?.Than);
    _smaller = condition?.Smaller ?? false;
    _same = condition?.Same ?? false;
    _bigger = condition?.Bigger ?? false;
    Mask = _value.Mask | _than.Mask;
  }

  /// <summary>The values either side uses.</summary>
  public OverlayValueMask Mask { get; }

  /// <summary>How the value compares now.</summary>
  public OverlayComparison Compare(IOverlayNumberSource source) =>
    Number(_value, source, out double value) && Number(_than, source, out double than)
      ? Compare(value, than)
      : OverlayComparison.Unknown;

  /// <summary>Whether the condition is on now; off while either side has no data.</summary>
  public bool Holds(IOverlayNumberSource source) => Turns(Compare(source), _smaller, _same, _bigger);

  /// <summary>Whether <paramref name="condition"/> is on when its value compares as <paramref name="result"/>.</summary>
  public static bool Turns(OverlayCondition condition, OverlayComparison result) =>
    condition != null && Turns(result, condition.Smaller, condition.Same, condition.Bigger);

  public static OverlayComparison Compare(double value, double than)
  {
    if (double.IsNaN(value) || double.IsNaN(than))
      return OverlayComparison.Unknown;
    double difference = value - than;
    double tolerance = 5e-7 * Math.Max(1, Math.Max(Math.Abs(value), Math.Abs(than)));
    return Math.Abs(difference) <= tolerance ? OverlayComparison.Same
      : difference < 0 ? OverlayComparison.Smaller
      : OverlayComparison.Bigger;
  }

  private static bool Turns(OverlayComparison result, bool smaller, bool same, bool bigger) =>
    result switch
    {
      OverlayComparison.Smaller => smaller,
      OverlayComparison.Same => same,
      OverlayComparison.Bigger => bigger,
      _ => false,
    };

  private static bool Number(OverlayFormula formula, IOverlayNumberSource source, out double number)
  {
    number = double.NaN;
    if (formula.IsEmpty)
      return false;
    return formula.IsPlainNumber(out number) || (source != null && formula.TryEvaluate(source, out number, out _));
  }
}
