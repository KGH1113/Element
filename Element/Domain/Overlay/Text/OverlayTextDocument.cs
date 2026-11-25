using System;
using System.Collections.Generic;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Text;

/// <summary>
/// The editable form of a text element. Positions are caret positions between
/// items, so a value is always selected, deleted and skipped as one character.
/// Colors are shared, never mutated; callers pass a fresh copy to change one.
/// </summary>
/// <remarks>
/// An If is a stretch of the items (<see cref="OverlayTextBlock"/>): the look
/// it shows sits among the items and is edited like the rest, while its other
/// looks wait in the block. Every edit keeps the stretches in place: text
/// typed inside one, at its end, or into one showing nothing belongs to it;
/// deleting a selection that holds a whole If and more takes the If away.
/// </remarks>
internal sealed class OverlayTextDocument
{
  public const int MaxItems = 500;
  private const int NoOwner = -1,
    AutoOwner = -2;
  private static readonly OverlayColorSettings DefaultColor = OverlayColorSettings.Custom("#FFFFFF");
  private readonly List<OverlayTextItem> _items = new();
  private readonly List<OverlayTextBlock> _blocks = new();

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
      if (run.If == null)
      {
        AppendRun(document._items, run);
        continue;
      }
      // An If starts on otherwise, which is what shows before a level has data.
      OverlayTextBlock block = new() { Start = document._items.Count };
      AppendRuns(document._items, run.If.Otherwise);
      block.End = document._items.Count;
      if (run.If.Cases != null)
        foreach (OverlayTextCase item in run.If.Cases)
        {
          if (item == null || block.Cases >= OverlayTextIf.MaximumCases)
            continue;
          List<OverlayTextItem> look = new();
          AppendRuns(look, item.Runs);
          block.Looks.Insert(block.Cases, look);
          block.Conditions.Add((item.When ?? new OverlayCondition()).Clone());
        }
      block.Shown = block.Otherwise;
      // Without a case it is plain text.
      if (block.Cases > 0)
        document._blocks.Add(block);
    }
    return document;
  }

  private static void AppendRuns(List<OverlayTextItem> items, List<OverlayTextRun> runs)
  {
    if (runs == null)
      return;
    foreach (OverlayTextRun run in runs)
      if (run != null && run.If == null)
        AppendRun(items, run);
  }

  private static void AppendRun(List<OverlayTextItem> items, OverlayTextRun run)
  {
    OverlayColorSettings color = (run.Color ?? DefaultColor).Clone();
    float size = ValidSize(run.Size);
    string sizeFormula = Formula(run.SizeFormula);
    string font = OverlayFontKey.Normalize(run.Font);
    if (run.IsValue)
    {
      string formula = Formula(run.Value);
      if (formula.Length > 0)
        items.Add(OverlayTextItem.ValueItem(formula, color, size, sizeFormula, font, run.Animate));
    }
    else
      AppendCharacters(items, run.Text, color, size, sizeFormula, font);
  }

  public List<OverlayTextRun> ToRuns()
  {
    List<OverlayTextRun> runs = new();
    int at = 0;
    foreach (OverlayTextBlock block in _blocks)
    {
      AppendRuns(runs, _items, at, block.Start);
      at = block.End;
      OverlayTextIf saved = new();
      bool shows = false;
      for (int look = 0; look <= block.Otherwise; look++)
      {
        List<OverlayTextRun> lookRuns = new();
        if (look == block.Shown)
          AppendRuns(lookRuns, _items, block.Start, block.End);
        else
          AppendRuns(lookRuns, block.Looks[look], 0, block.Looks[look].Count);
        shows |= lookRuns.Count > 0;
        if (look == block.Otherwise)
          saved.Otherwise = lookRuns;
        else
          saved.Cases.Add(new OverlayTextCase { When = block.Conditions[look].Clone(), Runs = lookRuns });
      }
      // An If that shows nothing in any case is nothing.
      if (shows)
        runs.Add(new OverlayTextRun { If = saved });
    }
    AppendRuns(runs, _items, at, _items.Count);
    return runs;
  }

  private static void AppendRuns(List<OverlayTextRun> runs, List<OverlayTextItem> items, int start, int end)
  {
    StringBuilder literal = new();
    OverlayTextItem? style = null;
    for (int i = start; i < end; i++)
    {
      OverlayTextItem item = items[i];
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
        run.Font = item.Font;
        run.Animate = item.Animate;
        runs.Add(run);
      }
      else
      {
        style = item;
        literal.Append(item.Text);
      }
    }
    Flush();

    void Flush()
    {
      if (style != null && literal.Length > 0)
      {
        OverlayTextRun run = OverlayTextRun.Literal(literal.ToString(), style.Value.Color.Clone());
        run.Size = style.Value.Size;
        run.SizeFormula = style.Value.SizeFormula;
        run.Font = style.Value.Font;
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
    foreach (OverlayTextBlock block in _blocks)
      copy._blocks.Add(block.Clone());
    return copy;
  }

  /// <summary>Whether both save as the same text, whichever look each If shows now.</summary>
  public bool SameAs(OverlayTextDocument other)
  {
    if (other == null)
      return false;
    if (_blocks.Count == 0 && other._blocks.Count == 0)
    {
      if (other.Count != Count)
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
    return OverlayTextSettings.SameRuns(ToRuns(), other.ToRuns());
  }

  /// <summary>Nothing to show in any case: no items, and no If with a look that holds any.</summary>
  public bool IsBlank
  {
    get
    {
      if (_items.Count > 0)
        return false;
      foreach (OverlayTextBlock block in _blocks)
      foreach (List<OverlayTextItem> look in block.Looks)
        if (look.Count > 0)
          return false;
      return true;
    }
  }

  /// <summary>Replaces each value <paramref name="literal"/> names with plain characters in its style, in every look.</summary>
  public void ReplaceValues(Func<string, string> literal)
  {
    List<OverlayTextItem> characters = new();
    for (int i = _items.Count - 1; i >= 0; i--)
      if (Characters(_items[i], literal, characters))
        Splice(i, 1, characters, characters.Count, Containing(i));
    foreach (OverlayTextBlock block in _blocks)
    foreach (List<OverlayTextItem> look in block.Looks)
      for (int i = look.Count - 1; i >= 0; i--)
        if (Characters(look[i], literal, characters))
        {
          look.RemoveAt(i);
          look.InsertRange(i, characters);
        }
    if (_items.Count > MaxItems)
      Splice(MaxItems, _items.Count - MaxItems, null, 0, NoOwner);
  }

  private static bool Characters(OverlayTextItem item, Func<string, string> literal, List<OverlayTextItem> output)
  {
    string text = item.IsValue ? literal(item.Value) : null;
    if (text == null)
      return false;
    output.Clear();
    AppendCharacters(output, text, item.Color, item.Size, item.SizeFormula, item.Font);
    return true;
  }

  /// <summary>Replaces the range with typed or pasted text; returns the caret after it.</summary>
  public int InsertText(
    int start,
    int end,
    string text,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null,
    string font = null
  )
  {
    List<OverlayTextItem> items = new();
    AppendCharacters(
      items,
      text,
      color ?? DefaultColor,
      ValidSize(size),
      Formula(sizeFormula),
      OverlayFontKey.Normalize(font)
    );
    return InsertItems(start, end, items);
  }

  /// <summary>Inserts a value (a saved formula such as a key); returns the caret after it.</summary>
  public int InsertValue(
    int start,
    int end,
    string formula,
    OverlayColorSettings color,
    float size = 0,
    string sizeFormula = null,
    string font = null,
    bool animate = false
  )
  {
    formula = Formula(formula);
    if (formula.Length == 0)
      return Clamp(start);
    return InsertItems(
      start,
      end,
      new[]
      {
        OverlayTextItem.ValueItem(
          formula,
          color ?? DefaultColor,
          ValidSize(size),
          Formula(sizeFormula),
          OverlayFontKey.Normalize(font),
          animate
        ),
      }
    );
  }

  /// <summary>Changes one value's formula; an empty formula removes the value.</summary>
  public void SetValue(int index, string formula)
  {
    if (index < 0 || index >= _items.Count || !_items[index].IsValue)
      return;
    formula = Formula(formula);
    if (formula.Length == 0)
      Splice(index, 1, null, 0, NoOwner);
    else
      _items[index] = _items[index].WithValue(formula);
  }

  public int InsertItems(int start, int end, IReadOnlyList<OverlayTextItem> items)
  {
    Order(ref start, ref end);
    int room = Math.Max(0, MaxItems - (_items.Count - (end - start)));
    int count = Math.Min(room, items.Count);
    Splice(start, end - start, items, count, AutoOwner);
    return start + count;
  }

  public void Delete(int start, int end)
  {
    Order(ref start, ref end);
    Splice(start, end - start, null, 0, NoOwner);
  }

  // ---- Ifs ----

  public int BlockCount => _blocks.Count;

  public OverlayTextBlock Block(int index) => _blocks[index];

  /// <summary>
  /// The first If the range touches, or with an empty range the one the caret
  /// is in or at an end of; -1 for none.
  /// </summary>
  public int BlockIn(int start, int end)
  {
    Order(ref start, ref end);
    for (int i = 0; i < _blocks.Count; i++)
    {
      OverlayTextBlock block = _blocks[i];
      bool touches =
        start == end || block.Start == block.End
          ? block.Start <= end && block.End >= start
          : block.Start < end && block.End > start;
      if (touches)
        return i;
    }
    return -1;
  }

  /// <summary>
  /// Makes the range an If with no case yet, to be given one at once
  /// (<see cref="AddCase"/>); its number, or -1 when the range touches an If
  /// already there.
  /// </summary>
  public int AddBlock(int start, int end)
  {
    Order(ref start, ref end);
    int at = 0;
    foreach (OverlayTextBlock other in _blocks)
    {
      bool touches =
        other.Start == other.End ? (start < other.Start && other.Start < end) || (start == end && start == other.Start)
        : start == end ? other.Start <= start && start <= other.End
        : other.Start < end && other.End > start;
      if (touches)
        return -1;
      if (other.End <= start)
        at++;
    }
    _blocks.Insert(at, new OverlayTextBlock { Start = start, End = end });
    return at;
  }

  /// <summary>
  /// Adds a case after the others, looking as the If does now, so nothing
  /// changes until that look is edited; its number, or -1 with no room for more.
  /// </summary>
  public int AddCase(int block, OverlayCondition when)
  {
    OverlayTextBlock target = _blocks[block];
    if (target.Cases >= OverlayTextIf.MaximumCases)
      return -1;
    int index = target.Cases;
    target.Looks.Insert(index, _items.GetRange(target.Start, target.End - target.Start));
    target.Conditions.Add((when ?? new OverlayCondition()).Clone());
    if (target.Shown >= index)
      target.Shown++;
    return index;
  }

  /// <summary>
  /// Takes a case and its look away. Without its last case the If is gone
  /// too, and what otherwise showed stays as plain text.
  /// </summary>
  public void RemoveCase(int block, int index)
  {
    OverlayTextBlock target = _blocks[block];
    if (index < 0 || index >= target.Cases)
      return;
    if (target.Shown == index)
      Show(block, target.Otherwise);
    target.Conditions.RemoveAt(index);
    target.Looks.RemoveAt(index);
    if (target.Shown > index)
      target.Shown--;
    if (target.Cases == 0)
      _blocks.RemoveAt(block);
  }

  public void SetCase(int block, int index, OverlayCondition when) =>
    _blocks[block].Conditions[index] = (when ?? new OverlayCondition()).Clone();

  /// <summary>Puts look <paramref name="look"/> of the If in the document, in place of the one shown.</summary>
  public void Show(int block, int look)
  {
    OverlayTextBlock target = _blocks[block];
    if (look == target.Shown || look < 0 || look > target.Otherwise)
      return;
    List<OverlayTextItem> shown = _items.GetRange(target.Start, target.End - target.Start);
    List<OverlayTextItem> next = target.Looks[look];
    _items.RemoveRange(target.Start, shown.Count);
    _items.InsertRange(target.Start, next);
    target.Looks[target.Shown] = shown;
    target.Looks[look] = new List<OverlayTextItem>();
    target.Shown = look;
    int moved = next.Count - shown.Count;
    target.End += moved;
    for (int i = block + 1; i < _blocks.Count; i++)
    {
      _blocks[i].Start += moved;
      _blocks[i].End += moved;
    }
  }

  /// <summary>How many items look <paramref name="look"/> of the If holds.</summary>
  public int LookCount(int block, int look)
  {
    OverlayTextBlock target = _blocks[block];
    return look == target.Shown ? target.End - target.Start : target.Looks[look].Count;
  }

  /// <summary>Item <paramref name="index"/> of look <paramref name="look"/> of the If.</summary>
  public OverlayTextItem LookItem(int block, int look, int index)
  {
    OverlayTextBlock target = _blocks[block];
    return look == target.Shown ? _items[target.Start + index] : target.Looks[look][index];
  }

  // The If item <paramref name="index"/> is in; none outside them all.
  private int Containing(int index)
  {
    for (int i = 0; i < _blocks.Count; i++)
      if (_blocks[i].Start <= index && index < _blocks[i].End)
        return i;
    return NoOwner;
  }

  // Every change to the items goes through here, so the Ifs keep their
  // places: <paramref name="remove"/> items go from <paramref name="start"/>,
  // then <paramref name="count"/> of <paramref name="insert"/> come in there.
  // They belong to the If <paramref name="owner"/> names, to none, or (auto)
  // to the one typing there would continue.
  private void Splice(int start, int remove, IReadOnlyList<OverlayTextItem> insert, int count, int owner)
  {
    int end = start + remove;
    _items.RemoveRange(start, remove);
    for (int i = _blocks.Count - 1; i >= 0; i--)
    {
      OverlayTextBlock block = _blocks[i];
      bool empty = block.Start == block.End;
      // A selection holding a whole If and more takes the If with it.
      bool gone =
        remove > 0
        && (
          empty
            ? start < block.Start && block.Start < end
            : start <= block.Start && block.End <= end && remove > block.End - block.Start
        );
      if (gone)
      {
        _blocks.RemoveAt(i);
        if (owner > i)
          owner--;
        else if (owner == i)
          owner = NoOwner;
        continue;
      }
      block.Start = After(block.Start, start, end, remove);
      block.End = After(block.End, start, end, remove);
    }
    if (count <= 0)
      return;
    if (owner == AutoOwner)
      owner = Typing(start);
    for (int i = 0; i < count; i++)
      _items.Insert(start + i, insert[i]);
    for (int i = 0; i < _blocks.Count; i++)
    {
      OverlayTextBlock block = _blocks[i];
      if (i == owner)
        block.End += count;
      else if (block.Start >= start)
      {
        block.Start += count;
        block.End += count;
      }
    }
  }

  private static int After(int position, int start, int end, int remove) =>
    position <= start ? position
    : position >= end ? position - remove
    : start;

  // The If that text put in at <paramref name="at"/> belongs to: one it is
  // inside, one showing nothing right there, one that ends there, or one
  // that starts the whole text; otherwise none.
  private int Typing(int at)
  {
    for (int i = 0; i < _blocks.Count; i++)
      if (_blocks[i].Start < at && at < _blocks[i].End)
        return i;
    for (int i = 0; i < _blocks.Count; i++)
      if (_blocks[i].Start == at && _blocks[i].End == at)
        return i;
    for (int i = 0; i < _blocks.Count; i++)
      if (_blocks[i].End == at && _blocks[i].Start < at)
        return i;
    return at == 0 && _blocks.Count > 0 && _blocks[0].Start == 0 ? 0 : NoOwner;
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

  /// <summary>
  /// Adds the values the range shows to <paramref name="output"/>, each once
  /// and in order; a calculation adds each value it uses.
  /// </summary>
  public void ValuesIn(int start, int end, List<OverlayValueId> output)
  {
    Order(ref start, ref end);
    for (int i = start; i < end; i++)
    {
      if (!_items[i].IsValue)
        continue;
      OverlayFormula formula = OverlayFormula.Parse(_items[i].Value);
      for (int t = 0; t < formula.Count; t++)
        if (formula[t].Kind == OverlayFormulaTokenKind.Value && !output.Contains(formula[t].Value))
          output.Add(formula[t].Value);
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

  /// <summary>Gives the range one font (an OverlayFontKey); empty follows the default font.</summary>
  public void SetFont(int start, int end, string font)
  {
    Order(ref start, ref end);
    font = OverlayFontKey.Normalize(font);
    for (int i = start; i < end; i++)
      _items[i] = _items[i].WithFont(font);
  }

  /// <summary>The range's font, or null when it mixes fonts or is empty.</summary>
  public string RangeFont(int start, int end)
  {
    Order(ref start, ref end);
    if (start == end)
      return null;
    string font = _items[start].Font;
    for (int i = start + 1; i < end; i++)
      if (_items[i].Font != font)
        return null;
    return font;
  }

  /// <summary>Turns animation on or off for the range's values; the text between them never moves.</summary>
  public void SetAnimate(int start, int end, bool animate)
  {
    Order(ref start, ref end);
    for (int i = start; i < end; i++)
      if (_items[i].IsValue)
        _items[i] = _items[i].WithAnimate(animate);
  }

  public OverlayAnimateState AnimateState(int start, int end)
  {
    Order(ref start, ref end);
    bool on = false,
      off = false;
    for (int i = start; i < end; i++)
      if (_items[i].IsValue)
      {
        on |= _items[i].Animate;
        off |= !_items[i].Animate;
      }
    return on && off ? OverlayAnimateState.Mixed
      : on ? OverlayAnimateState.On
      : off ? OverlayAnimateState.Off
      : OverlayAnimateState.NoValues;
  }

  /// <summary>A new value animates like the nearest value, before the caret first; with none, it doesn't.</summary>
  public bool TypingAnimate(int caret)
  {
    caret = Clamp(caret);
    for (int i = caret - 1; i >= 0; i--)
      if (_items[i].IsValue)
        return _items[i].Animate;
    for (int i = caret; i < _items.Count; i++)
      if (_items[i].IsValue)
        return _items[i].Animate;
    return false;
  }

  /// <summary>New typing continues the nearest literal text's color, size and font.</summary>
  public OverlayColorSettings TypingColor(int caret) => TypingItem(caret)?.Color ?? DefaultColor;

  public float TypingSize(int caret) => TypingItem(caret)?.Size ?? 0;

  public string TypingSizeFormula(int caret) => TypingItem(caret)?.SizeFormula ?? string.Empty;

  public string TypingFont(int caret) => TypingItem(caret)?.Font ?? string.Empty;

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
    string sizeFormula = null,
    string font = null
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
        items.Add(OverlayTextItem.Character(text.Substring(i, 2), color, size, sizeFormula, font));
        i++;
        continue;
      }
      if (char.IsSurrogate(c) || (char.IsControl(c) && c != '\n' && c != '\t'))
        continue;
      items.Add(OverlayTextItem.Character(c.ToString(), color, size, sizeFormula, font));
    }
  }
}
