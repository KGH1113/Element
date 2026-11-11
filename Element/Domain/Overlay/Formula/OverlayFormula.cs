using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Values;

namespace Element.Domain.Overlay.Formula;

/// <summary>
/// Numbers and values joined by + − × ÷, with × and ÷ first. Words such as
/// the song title can only stand alone. <see cref="OverlayFormulaSyntax"/>
/// reads and saves it, <see cref="OverlayFormulaKeypad"/> edits it,
/// <see cref="OverlayFormulaEvaluator"/> works it out and
/// <see cref="OverlayFormulaDisplay"/> puts it into words; the methods here
/// hand over to them.
/// </summary>
internal sealed class OverlayFormula
{
  public const int MaxTokens = 31;
  public const int MaxDigits = 9;
  private readonly List<OverlayFormulaToken> _tokens = new();

  public int Count => _tokens.Count;
  public OverlayFormulaToken this[int index] => _tokens[index];
  public bool IsEmpty => _tokens.Count == 0;

  /// <summary>The last step is an operator, so the next number or value joins the formula.</summary>
  public bool AwaitsOperand => _tokens.Count > 0 && _tokens[_tokens.Count - 1].Kind == OverlayFormulaTokenKind.Operator;

  /// <summary>The last step, or null when the formula is empty.</summary>
  public OverlayFormulaToken? Last => _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;

  /// <summary>Empty, or ending in an operator: a number or value can come next.</summary>
  public bool CanTakeOperand => _tokens.Count == 0 || AwaitsOperand;

  public static OverlayFormula Parse(string text) => OverlayFormulaSyntax.Parse(text);

  public static OverlayFormula FromValue(OverlayValueId id)
  {
    OverlayFormula formula = new();
    formula._tokens.Add(OverlayFormulaToken.ValueToken(id));
    return formula;
  }

  public static OverlayFormula FromNumber(double number)
  {
    OverlayFormula formula = new();
    formula._tokens.Add(OverlayFormulaToken.NumberToken(NumberText(number)));
    return formula;
  }

  public OverlayFormula Clone()
  {
    OverlayFormula copy = new();
    copy._tokens.AddRange(_tokens);
    return copy;
  }

  public string Serialize() => OverlayFormulaSyntax.Serialize(this);

  public bool IsSingleValue(out OverlayValueId id)
  {
    id = default;
    if (_tokens.Count != 1 || _tokens[0].Kind != OverlayFormulaTokenKind.Value)
      return false;
    id = _tokens[0].Value;
    return true;
  }

  /// <summary>One plain number: a field holding it keeps the number, not a formula.</summary>
  public bool IsPlainNumber(out double number)
  {
    number = 0;
    if (_tokens.Count != 1 || _tokens[0].Kind != OverlayFormulaTokenKind.Number || !_tokens[0].HasNumber)
      return false;
    number = _tokens[0].NumberValue;
    return true;
  }

  public bool HasValues
  {
    get
    {
      foreach (OverlayFormulaToken token in _tokens)
        if (token.Kind == OverlayFormulaTokenKind.Value)
          return true;
      return false;
    }
  }

  /// <summary>Words (the song title, a label) stand alone; arithmetic can't use them.</summary>
  public bool IsWords => IsSingleValue(out OverlayValueId id) && !OverlayValueCatalog.Get(id).IsNumeric;

  public OverlayValueMask Mask
  {
    get
    {
      OverlayValueMask mask = OverlayValueMask.None;
      foreach (OverlayFormulaToken token in _tokens)
        if (token.Kind == OverlayFormulaTokenKind.Value)
          mask |= OverlayValueCatalog.Bit(token.Value);
      return mask;
    }
  }

  public bool SameAs(OverlayFormula other)
  {
    if (other == null || other._tokens.Count != _tokens.Count)
      return false;
    for (int i = 0; i < _tokens.Count; i++)
    {
      OverlayFormulaToken a = _tokens[i],
        b = other._tokens[i];
      if (a.Kind != b.Kind || a.Number != b.Number || a.Value != b.Value || a.Operator != b.Operator)
        return false;
    }
    return true;
  }

  public void Add(OverlayFormulaToken token) => _tokens.Add(token);

  public void SetLast(OverlayFormulaToken token) => _tokens[_tokens.Count - 1] = token;

  public void RemoveAt(int index) => _tokens.RemoveAt(index);

  public void Clear() => _tokens.Clear();

  /// <inheritdoc cref="OverlayFormulaKeypad.CanType"/>
  public bool CanType(char key) => OverlayFormulaKeypad.CanType(this, key);

  /// <inheritdoc cref="OverlayFormulaKeypad.Type"/>
  public bool Type(char key) => OverlayFormulaKeypad.Type(this, key);

  public bool CanBackspace => _tokens.Count > 0;

  /// <inheritdoc cref="OverlayFormulaKeypad.Backspace"/>
  public bool Backspace() => OverlayFormulaKeypad.Backspace(this);

  /// <inheritdoc cref="OverlayFormulaKeypad.CanJoin"/>
  public bool CanJoin(OverlayValueId id) => OverlayFormulaKeypad.CanJoin(this, id);

  /// <inheritdoc cref="OverlayFormulaKeypad.Drop"/>
  public void Drop(OverlayValueId id) => OverlayFormulaKeypad.Drop(this, id);

  /// <inheritdoc cref="OverlayFormulaKeypad.Trimmed"/>
  public OverlayFormula Trimmed() => OverlayFormulaKeypad.Trimmed(this);

  /// <inheritdoc cref="OverlayFormulaEvaluator.TryEvaluate"/>
  public bool TryEvaluate(IOverlayNumberSource source, out double result, out bool isTime) =>
    OverlayFormulaEvaluator.TryEvaluate(this, source, out result, out isTime);

  /// <inheritdoc cref="OverlayFormulaDisplay.Describe"/>
  public string Describe(Func<OverlayValueDefinition, string> name) => OverlayFormulaDisplay.Describe(this, name);

  public static char Symbol(char op) => OverlayFormulaDisplay.Symbol(op);

  /// <inheritdoc cref="OverlayFormulaSyntax.NormalizeOperator"/>
  public static char NormalizeOperator(char c) => OverlayFormulaSyntax.NormalizeOperator(c);

  public static string NumberText(double number) => OverlayFormulaSyntax.NumberText(number);
}
