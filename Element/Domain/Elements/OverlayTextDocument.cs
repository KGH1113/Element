using System;
using System.Collections.Generic;
using System.Text;
using Element.Configuration;

namespace Element.Domain.Elements;

// One caret step: a displayed character (one or two UTF-16 units) or a whole
// value, with its color and size (zero for the text's base size). A value is
// a saved formula (OverlayFormula), usually one value's key. A size formula,
// when set, gives the size in pixels as drawn and replaces Size.
internal readonly struct OverlayTextItem
{
  public readonly string Text;
  public readonly string Value;
  public readonly OverlayColorSettings Color;
  public readonly float Size;
  public readonly string SizeFormula;
  public bool IsValue => Value != null;

  private OverlayTextItem(string text, string value, OverlayColorSettings color, float size, string sizeFormula)
  {
    Text = text;
    Value = value;
    Color = color;
    Size = size;
    SizeFormula = sizeFormula ?? string.Empty;
  }

  public static OverlayTextItem Character(
    string text,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null
  ) => new(text, null, color, size, sizeFormula);

  public static OverlayTextItem ValueItem(
    string formula,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null
  ) => new(null, formula, color, size, sizeFormula);

  public OverlayTextItem WithColor(OverlayColorSettings color) => new(Text, Value, color, Size, SizeFormula);

  public OverlayTextItem WithSize(float size, string sizeFormula = null) => new(Text, Value, Color, size, sizeFormula);

  public OverlayTextItem WithValue(string formula) => new(Text, formula, Color, Size, SizeFormula);

  public bool SameStyle(OverlayTextItem other) =>
    Size == other.Size && SizeFormula == other.SizeFormula && Color.SameAs(other.Color);
}

/// <summary>
/// The editable form of a text element. Positions are caret positions between
/// items, so a value is always selected, deleted and skipped as one character.
/// Colors are shared, never mutated; callers pass a fresh copy to change one.
/// </summary>
internal sealed class OverlayTextDocument
{
  public const int MaxItems = 500;
  private static readonly OverlayColorSettings DefaultColor = OverlayColorSettings.Custom("#FFFFFF");
  private readonly List<OverlayTextItem> _items = new();

  public int Count => _items.Count;
  public OverlayTextItem this[int index] => _items[index];

  public static OverlayTextDocument FromRuns(IEnumerable<OverlayTextRun> runs)
  {
    OverlayTextDocument document = new();
    if (runs == null)
      return document;
    foreach (OverlayTextRun run in runs)
    {
      if (run == null)
        continue;
      OverlayColorSettings color = (run.Color ?? DefaultColor).Clone();
      float size = ValidSize(run.Size);
      string sizeFormula = Formula(run.SizeFormula);
      if (run.IsValue)
      {
        string formula = Formula(run.Value);
        if (formula.Length > 0)
          document._items.Add(OverlayTextItem.ValueItem(formula, color, size, sizeFormula));
      }
      else
        AppendCharacters(document._items, run.Text, color, size, sizeFormula);
    }
    return document;
  }

  public List<OverlayTextRun> ToRuns()
  {
    List<OverlayTextRun> runs = new();
    StringBuilder literal = new();
    OverlayTextItem? style = null;
    foreach (OverlayTextItem item in _items)
    {
      if (!item.IsValue && style != null && item.SameStyle(style.Value))
      {
        literal.Append(item.Text);
        continue;
      }
      Flush();
      if (item.IsValue)
      {
        OverlayTextRun run = OverlayTextRun.ValueRun(item.Value, item.Color.Clone());
        run.Size = item.Size;
        run.SizeFormula = item.SizeFormula;
        runs.Add(run);
      }
      else
      {
        style = item;
        literal.Append(item.Text);
      }
    }
    Flush();
    return runs;

    void Flush()
    {
      if (style != null && literal.Length > 0)
      {
        OverlayTextRun run = OverlayTextRun.Literal(literal.ToString(), style.Value.Color.Clone());
        run.Size = style.Value.Size;
        run.SizeFormula = style.Value.SizeFormula;
        runs.Add(run);
      }
      literal.Clear();
      style = null;
    }
  }

  public OverlayTextDocument Clone()
  {
    OverlayTextDocument copy = new();
    copy._items.AddRange(_items);
    return copy;
  }

  public bool SameAs(OverlayTextDocument other)
  {
    if (other == null || other.Count != Count)
      return false;
    for (int i = 0; i < Count; i++)
    {
      OverlayTextItem a = _items[i],
        b = other._items[i];
      if (a.Text != b.Text || a.Value != b.Value || !a.SameStyle(b))
        return false;
    }
    return true;
  }

  /// <summary>Replaces each value <paramref name="literal"/> names with plain characters in its style.</summary>
  public void ReplaceValues(Func<string, string> literal)
  {
    for (int i = _items.Count - 1; i >= 0; i--)
    {
      OverlayTextItem item = _items[i];
      string text = item.IsValue ? literal(item.Value) : null;
      if (text == null)
        continue;
      List<OverlayTextItem> characters = new();
      AppendCharacters(characters, text, item.Color, item.Size, item.SizeFormula);
      _items.RemoveAt(i);
      _items.InsertRange(i, characters);
    }
    if (_items.Count > MaxItems)
      _items.RemoveRange(MaxItems, _items.Count - MaxItems);
  }

  /// <summary>Replaces the range with typed or pasted text; returns the caret after it.</summary>
  public int InsertText(
    int start,
    int end,
    string text,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null
  )
  {
    List<OverlayTextItem> items = new();
    AppendCharacters(items, text, color ?? DefaultColor, ValidSize(size), Formula(sizeFormula));
    return InsertItems(start, end, items);
  }

  /// <summary>Inserts a value (a saved formula such as a key); returns the caret after it.</summary>
  public int InsertValue(
    int start,
    int end,
    string formula,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null
  )
  {
    formula = Formula(formula);
    if (formula.Length == 0)
      return Clamp(start);
    return InsertItems(
      start,
      end,
      new[] { OverlayTextItem.ValueItem(formula, color ?? DefaultColor, ValidSize(size), Formula(sizeFormula)) }
    );
  }

  /// <summary>Changes one value's formula; an empty formula removes the value.</summary>
  public void SetValue(int index, string formula)
  {
    if (index < 0 || index >= _items.Count || !_items[index].IsValue)
      return;
    formula = Formula(formula);
    if (formula.Length == 0)
      _items.RemoveAt(index);
    else
      _items[index] = _items[index].WithValue(formula);
  }

  public int InsertItems(int start, int end, IReadOnlyList<OverlayTextItem> items)
  {
    Order(ref start, ref end);
    _items.RemoveRange(start, end - start);
    int room = Math.Max(0, MaxItems - _items.Count);
    int count = Math.Min(room, items.Count);
    for (int i = 0; i < count; i++)
      _items.Insert(start + i, items[i]);
    return start + count;
  }

  public void Delete(int start, int end)
  {
    Order(ref start, ref end);
    _items.RemoveRange(start, end - start);
  }

  public List<OverlayTextItem> Slice(int start, int end)
  {
    Order(ref start, ref end);
    return _items.GetRange(start, end - start);
  }

  public void SetColor(int start, int end, OverlayColorSettings color)
  {
    Order(ref start, ref end);
    for (int i = start; i < end; i++)
      _items[i] = _items[i].WithColor(color.Clone());
  }

  /// <summary>Changes only the opacity of each item's color; formulas give percent.</summary>
  public void SetAlpha(int start, int end, float alpha, string alphaFormula = null)
  {
    Order(ref start, ref end);
    alphaFormula = Formula(alphaFormula);
    for (int i = start; i < end; i++)
    {
      OverlayColorSettings color = _items[i].Color.Clone();
      color.Alpha = alpha;
      color.AlphaFormula = alphaFormula;
      _items[i] = _items[i].WithColor(color);
    }
  }

  public void SetSize(int start, int end, float size, string sizeFormula = null)
  {
    Order(ref start, ref end);
    size = ValidSize(size);
    sizeFormula = Formula(sizeFormula);
    for (int i = start; i < end; i++)
      _items[i] = _items[i].WithSize(size, sizeFormula);
  }

  /// <summary>New typing continues the nearest literal text's color and size.</summary>
  public OverlayColorSettings TypingColor(int caret) => TypingItem(caret)?.Color ?? DefaultColor;

  public float TypingSize(int caret) => TypingItem(caret)?.Size ?? 0;

  public string TypingSizeFormula(int caret) => TypingItem(caret)?.SizeFormula ?? string.Empty;

  private OverlayTextItem? TypingItem(int caret)
  {
    caret = Clamp(caret);
    for (int i = caret - 1; i >= 0; i--)
      if (!_items[i].IsValue)
        return _items[i];
    for (int i = caret; i < _items.Count; i++)
      if (!_items[i].IsValue)
        return _items[i];
    return null;
  }

  public int WordStart(int caret)
  {
    caret = Clamp(caret);
    while (caret > 0 && IsSpace(caret - 1))
      caret--;
    if (caret > 0 && !IsWordCharacter(caret - 1))
      return caret - 1;
    while (caret > 0 && IsWordCharacter(caret - 1))
      caret--;
    return caret;
  }

  public int WordEnd(int caret)
  {
    caret = Clamp(caret);
    while (caret < _items.Count && IsSpace(caret))
      caret++;
    if (caret < _items.Count && !IsWordCharacter(caret))
      return caret + 1;
    while (caret < _items.Count && IsWordCharacter(caret))
      caret++;
    return caret;
  }

  public string PlainText(int start, int end, Func<string, string> valueText)
  {
    Order(ref start, ref end);
    StringBuilder builder = new();
    for (int i = start; i < end; i++)
      builder.Append(_items[i].IsValue ? valueText(_items[i].Value) : _items[i].Text);
    return builder.ToString();
  }

  public int Clamp(int caret) => Math.Max(0, Math.Min(_items.Count, caret));

  private void Order(ref int start, ref int end)
  {
    start = Clamp(start);
    end = Clamp(end);
    if (end < start)
      (start, end) = (end, start);
  }

  private static float ValidSize(float size) => float.IsNaN(size) || float.IsInfinity(size) || size <= 0 ? 0 : size;

  // The saved form of a formula, or empty when it holds nothing usable.
  private static string Formula(string text)
  {
    if (string.IsNullOrEmpty(text))
      return string.Empty;
    OverlayFormula formula = OverlayFormula.Parse(text);
    return formula.IsEmpty ? string.Empty : formula.Serialize();
  }

  // Values and punctuation are words of their own.
  private bool IsWordCharacter(int index) =>
    !_items[index].IsValue && (char.IsLetterOrDigit(_items[index].Text, 0) || _items[index].Text[0] == '_');

  private bool IsSpace(int index) => !_items[index].IsValue && char.IsWhiteSpace(_items[index].Text, 0);

  private static void AppendCharacters(
    List<OverlayTextItem> items,
    string text,
    OverlayColorSettings color,
    float size,
    string sizeFormula = null
  )
  {
    if (string.IsNullOrEmpty(text))
      return;
    for (int i = 0; i < text.Length; i++)
    {
      char c = text[i];
      if (c == '\r')
      {
        if (i + 1 < text.Length && text[i + 1] == '\n')
          continue;
        c = '\n';
      }
      if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
      {
        items.Add(OverlayTextItem.Character(text.Substring(i, 2), color, size, sizeFormula));
        i++;
        continue;
      }
      if (char.IsSurrogate(c) || (char.IsControl(c) && c != '\n' && c != '\t'))
        continue;
      items.Add(OverlayTextItem.Character(c.ToString(), color, size, sizeFormula));
    }
  }
}
