using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// The variable the dock has open: the field's (<see cref="Slot"/>), and the
/// path into it, a condition's slot or a group being a variable of its own
/// (<see cref="Path"/>). One piece of the innermost is picked; values go in
/// after it or in its place, the keypad types into a number, and what the
/// picked piece is decides the input method. Every change is checked
/// against what the field must give: the field takes it while it fits, and
/// keeps what it had while it doesn't (<see cref="Problem"/>). The dock and
/// its parts read it and follow each change by themselves.
/// </summary>
internal sealed class FormulaEditing : Observable
{
  private readonly List<FormulaFrame> _path = new();
  private FormulaSlot _slot;
  private OverlayVariable _root;
  private int _selected = -1;
  private bool _typed;
  private OverlayTypeProblem _problem;

  public FormulaSlot Slot
  {
    get
    {
      Reading();
      return _slot;
    }
  }

  public bool Active
  {
    get
    {
      Reading();
      return _slot != null;
    }
  }

  /// <summary>The innermost open variable: the field's, or a slot inside it.</summary>
  public OverlayVariable Working
  {
    get
    {
      Reading();
      return _path.Count > 0 ? _path[_path.Count - 1].Variable : null;
    }
  }

  /// <summary>The field's whole variable.</summary>
  public OverlayVariable Root
  {
    get
    {
      Reading();
      return _root;
    }
  }

  /// <summary>Where the dock is: the field first, then each slot opened inside it.</summary>
  public IReadOnlyList<FormulaFrame> Path
  {
    get
    {
      Reading();
      return _path;
    }
  }

  /// <summary>What the innermost open variable must give.</summary>
  public OverlayValueType Kind
  {
    get
    {
      Reading();
      return _path.Count > 0 ? _path[_path.Count - 1].Type : OverlayValueType.Number;
    }
  }

  /// <summary>The picked piece's index in <see cref="Working"/>; -1 with none (values go at the end).</summary>
  public int Selected
  {
    get
    {
      Reading();
      return _selected;
    }
  }

  /// <summary>The picked piece; null with none.</summary>
  public OverlayPiece SelectedPiece
  {
    get
    {
      Reading();
      OverlayVariable working = _path.Count > 0 ? _path[_path.Count - 1].Variable : null;
      return working != null && _selected >= 0 && _selected < working.Count ? working[_selected] : null;
    }
  }

  /// <summary>Why the field's variable can't be what the field needs; null while it can.</summary>
  public OverlayTypeProblem Problem
  {
    get
    {
      Reading();
      return _problem;
    }
  }

  /// <summary>Something was typed or put in since the field opened; a variable only read back is not.</summary>
  public bool Typed
  {
    get
    {
      Reading();
      return _typed;
    }
  }

  // ---- Opening ----

  public void Activate(FormulaSlot slot)
  {
    if (slot == null)
    {
      Deactivate();
      return;
    }
    // The same field keeps where it is and what is picked; undo deactivates
    // first, so a restored variable is read afresh.
    bool same = _slot != null && _slot.Key == slot.Key && _root != null;
    _slot = slot;
    if (same)
      return;
    _root = slot.Read().Clone();
    _path.Clear();
    _path.Add(new FormulaFrame(_root, slot.Type, -1, slot.IsText, null));
    _selected = _root.Count - 1;
    _typed = false;
    _problem = Check();
    Changed();
  }

  public void Deactivate()
  {
    if (_slot == null && _root == null)
      return;
    _slot = null;
    _root = null;
    _path.Clear();
    _selected = -1;
    _problem = null;
    Changed();
  }

  // ---- Moving around ----

  /// <summary>Picks piece <paramref name="index"/> of the innermost variable; a second click on it lets go.</summary>
  public void Select(int index)
  {
    if (_path.Count == 0)
      return;
    OverlayVariable working = _path[_path.Count - 1].Variable;
    _selected = index >= 0 && index < working.Count && index != _selected ? index : -1;
    Changed();
  }

  /// <summary>Whether the picked piece has a variable to go into: a condition's slot or a group's.</summary>
  public bool CanEnter(int slot)
  {
    OverlayPiece piece = SelectedPiece;
    return piece != null && slot >= 0 && slot < piece.Slots.Length;
  }

  /// <summary>
  /// What slot <paramref name="slot"/> of the picked condition must give: a
  /// number where it compares, a comparison to compare by, and for its
  /// result and default whatever the condition must give where it sits.
  /// </summary>
  public OverlayValueType SlotType(int slot)
  {
    Reading();
    return slot == OverlayPiece.Compare ? OverlayValueType.Compare
      : slot == OverlayPiece.Left || slot == OverlayPiece.Right ? OverlayValueType.Number
      : NeededAt(_selected, replacing: true);
  }

  /// <summary>Goes into slot <paramref name="slot"/> of the picked condition or group.</summary>
  public void Enter(int slot)
  {
    if (!CanEnter(slot))
      return;
    Open(slot);
    Changed();
  }

  /// <summary>
  /// Picks piece <paramref name="index"/> of the variable <paramref name="route"/>
  /// leads to, from the field's, in one go: every condition's slot is laid out
  /// on the line, so any of them is a click away. -1 picks the variable
  /// itself, where a value then goes in at its end.
  /// </summary>
  public void SelectAt(IReadOnlyList<FormulaStep> route, int index)
  {
    if (_path.Count == 0)
      return;
    while (_path.Count > 1)
      _path.RemoveAt(_path.Count - 1);
    if (route != null)
      foreach (FormulaStep step in route)
      {
        OverlayVariable here = _path[_path.Count - 1].Variable;
        if (step.Piece < 0 || step.Piece >= here.Count)
          break;
        _selected = step.Piece;
        if (!CanEnter(step.Slot))
          break;
        Open(step.Slot);
      }
    OverlayVariable working = _path[_path.Count - 1].Variable;
    _selected = index >= 0 && index < working.Count ? index : -1;
    Changed();
  }

  // Goes one variable in, without telling the readers yet.
  private void Open(int slot)
  {
    OverlayPiece piece = SelectedPiece;
    OverlayValueType around = NeededAt(_selected, replacing: true);
    OverlayValueType type =
      piece.Kind == OverlayPieceKind.Group ? around
      : slot == OverlayPiece.Compare ? OverlayValueType.Compare
      : slot == OverlayPiece.Left || slot == OverlayPiece.Right ? OverlayValueType.Number
      : around;
    bool mayBeEmpty =
      piece.Kind == OverlayPieceKind.Condition && slot >= OverlayPiece.Result && around == OverlayValueType.Any;
    OverlayVariable inner = piece.Slots[slot];
    _path.Add(new FormulaFrame(inner, type, slot, mayBeEmpty, piece) { Outer = _selected });
    _selected = inner.Count - 1;
  }

  /// <summary>Back out of the innermost slot, the piece it belongs to picked again.</summary>
  public bool Back()
  {
    if (_path.Count <= 1)
      return false;
    FormulaFrame left = _path[_path.Count - 1];
    _path.RemoveAt(_path.Count - 1);
    _selected = left.Outer;
    Changed();
    return true;
  }

  /// <summary>Back out to depth <paramref name="depth"/> of the path (0 is the field).</summary>
  public void BackTo(int depth)
  {
    while (_path.Count > depth + 1 && Back()) { }
  }

  // ---- Inside a condition ----

  /// <summary>How many conditions' slots the dock went into, one inside another; 0 at the field.</summary>
  public int Depth
  {
    get
    {
      Reading();
      int depth = 0;
      for (int i = 1; i < _path.Count; i++)
        if (_path[i].Owner?.Kind == OverlayPieceKind.Condition)
          depth++;
      return depth;
    }
  }

  /// <summary>The condition whose slot the dock is in, the nearest one; null at the field.</summary>
  public OverlayPiece Around
  {
    get
    {
      Reading();
      int held = Held();
      return held > 0 ? _path[held].Owner : null;
    }
  }

  /// <summary>Which of <see cref="Around"/>'s slots the dock is in; -1 at the field.</summary>
  public int AroundSlot
  {
    get
    {
      Reading();
      int held = Held();
      return held > 0 ? _path[held].Slot : -1;
    }
  }

  /// <summary>What <see cref="Around"/>'s answers must give, where that condition sits.</summary>
  public OverlayValueType AroundGives
  {
    get
    {
      Reading();
      int held = Held();
      if (held == 0)
        return Kind;
      FormulaFrame outside = _path[held - 1];
      return InCalculation(outside.Variable, _path[held].Outer) ? OverlayValueType.Number : outside.Type;
    }
  }

  // The innermost step of the path that is a condition's slot; 0 for none.
  private int Held()
  {
    for (int i = _path.Count - 1; i > 0; i--)
      if (_path[i].Owner?.Kind == OverlayPieceKind.Condition)
        return i;
    return 0;
  }

  /// <summary>
  /// One step back out: out of the slot the dock is in, as far as the nearest
  /// condition, which is picked again. False at the field, with no further out.
  /// </summary>
  public bool Leave()
  {
    if (_path.Count <= 1)
      return false;
    Unwind();
    Changed();
    return true;
  }

  /// <summary>
  /// From inside a condition's slot straight into slot <paramref name="slot"/>
  /// of the same condition, without coming out in between.
  /// </summary>
  public bool Hop(int slot)
  {
    if (Held() == 0)
      return false;
    Unwind();
    if (CanEnter(slot))
      Open(slot);
    Changed();
    return true;
  }

  // Out to the nearest condition, through any group inside its slot, without telling the readers yet.
  private void Unwind()
  {
    while (_path.Count > 1)
    {
      FormulaFrame left = _path[_path.Count - 1];
      _path.RemoveAt(_path.Count - 1);
      _selected = left.Outer;
      if (left.Owner?.Kind == OverlayPieceKind.Condition)
        break;
    }
  }

  /// <summary>
  /// Goes to where what is in the way (<see cref="Problem"/>) can be seen:
  /// a condition's empty slot shows on that condition, so it is picked,
  /// however far in or out it is. Where it already shows, the dock stays.
  /// </summary>
  public void FaceProblem()
  {
    OverlayPiece at = _problem?.Condition;
    if (at == null || _root == null || at == SelectedPiece || at == Around)
      return;
    List<FormulaStep> route = new();
    if (Find(_root, at, route, out int index))
      SelectAt(route, index);
  }

  // The way from a variable to a piece somewhere in it: each step a piece and the slot of it gone into.
  private static bool Find(OverlayVariable variable, OverlayPiece piece, List<FormulaStep> route, out int index)
  {
    for (int i = 0; i < variable.Count; i++)
    {
      if (variable[i] == piece)
      {
        index = i;
        return true;
      }
      OverlayVariable[] slots = variable[i].Slots;
      for (int slot = 0; slot < slots.Length; slot++)
      {
        route.Add(new FormulaStep(i, slot));
        if (Find(slots[slot], piece, route, out index))
          return true;
        route.RemoveAt(route.Count - 1);
      }
    }
    index = -1;
    return false;
  }

  // ---- The keypad ----

  public bool CanPress(char key)
  {
    if (!Active)
      return false;
    OverlayVariable working = Working;
    OverlayValueType kind = Kind;
    OverlayPiece picked = SelectedPiece;
    if (kind == OverlayValueType.Compare)
      // A comparison key changes the picked comparison, or puts one in an empty slot; a condition there is picked away first.
      return IsCompare(key)
        && (picked != null ? picked.Kind == OverlayPieceKind.Compare && picked.Op != key : working.Count == 0);
    if (IsCompare(key))
      return false;
    if (kind != OverlayValueType.Number && kind != OverlayValueType.Any)
      return false;
    if (char.IsDigit(key) || key == '.')
    {
      if (picked != null && picked.Kind == OverlayPieceKind.Number)
        return key == '.'
          ? picked.Text.IndexOf('.') < 0 && Digits(picked.Text) < OverlayVariable.MaxDigits
          : Digits(picked.Text) < OverlayVariable.MaxDigits;
      // A new number starts in an empty variable or after an operator; a
      // value picked is joined with an operator first, never typed over.
      OverlayPiece last = picked ?? working.Last;
      return (last == null || last.Kind == OverlayPieceKind.Operator) && working.Count < OverlayVariable.MaxPieces;
    }
    char op = OverlayVariable.NormalizeOperator(key);
    if (op == '\0')
      return false;
    if (picked == null && working.Count == 0)
      return op == '-';
    if (picked != null && picked.Kind == OverlayPieceKind.Operator)
      return picked.Op != op;
    OverlayPiece before = picked ?? working.Last;
    // A lone "-" needs its digits first; words and colors can't be calculated with.
    if (before == null || (before.Kind == OverlayPieceKind.Number && !before.HasNumber))
      return false;
    return Calculable(before) && working.Count < OverlayVariable.MaxPieces;
  }

  /// <summary>Applies a keypad key: digits type into the picked number, an operator joins the next number to it.</summary>
  public bool Press(char key)
  {
    if (!CanPress(key))
      return false;
    OverlayVariable working = Working;
    OverlayPiece picked = SelectedPiece;
    if (Kind == OverlayValueType.Compare)
    {
      if (picked != null)
        picked.Op = key;
      else
      {
        working.Add(OverlayPiece.ComparePiece(key));
        _selected = 0;
      }
      Commit();
      return true;
    }
    if (char.IsDigit(key) || key == '.')
    {
      if (picked != null && picked.Kind == OverlayPieceKind.Number)
      {
        string text = picked.Text;
        // "0" then "7" is "7", not "07".
        if (key != '.' && (text == "0" || text == "-0"))
          text = text.Substring(0, text.Length - 1);
        picked.Text = text + (key == '.' && (text.Length == 0 || text[text.Length - 1] == '-') ? "0." : key.ToString());
      }
      else
        Put(OverlayPiece.NumberPiece(key == '.' ? "0." : key.ToString()));
      Commit();
      return true;
    }
    char op = OverlayVariable.NormalizeOperator(key);
    if (picked == null && working.Count == 0)
    {
      // A leading minus starts a negative number.
      InsertAfter(OverlayPiece.NumberPiece("-"));
      Commit();
      return true;
    }
    if (picked != null && picked.Kind == OverlayPieceKind.Operator)
      picked.Op = op;
    else
    {
      // An operator brings the number after it, picked to be typed over, so
      // a calculation never ends in an operator.
      InsertAfter(OverlayPiece.OperatorPiece(op));
      InsertAfter(OverlayPiece.NumberPiece("0"));
    }
    Commit();
    return true;
  }

  public bool CanBackspace => Active && SelectedPiece is { Kind: OverlayPieceKind.Number };

  /// <summary>
  /// Takes the picked number's last digit away: its last digit leaves 0, and
  /// 0 goes with the number (<see cref="Remove"/>).
  /// </summary>
  public bool Backspace()
  {
    if (!CanBackspace)
      return false;
    OverlayPiece piece = SelectedPiece;
    string text = piece.Text;
    if (text.Length > 1 && text != "-0")
      piece.Text = text.Substring(0, text.Length - 1);
    else if (text != "0")
      piece.Text = "0";
    else
      return Remove();
    if (piece.Text == "-")
      piece.Text = "0";
    Commit();
    return true;
  }

  /// <summary>
  /// Whether a picked piece can be taken out: anything but what a
  /// comparison's or a point's field holds, which is swapped for another,
  /// never left empty.
  /// </summary>
  public bool CanRemove =>
    Active && SelectedPiece != null && Kind != OverlayValueType.Compare && Kind != OverlayValueType.Point;

  /// <summary>
  /// Takes the picked piece out (지우기). In a calculation its operator goes
  /// with it, so what is left still calculates: the number after an
  /// operator takes that operator, a picked operator takes the number after it.
  /// </summary>
  public bool Remove()
  {
    if (!CanRemove)
      return false;
    OverlayVariable working = Working;
    int index = _selected;
    if (working[index].Kind == OverlayPieceKind.Operator)
    {
      int count = index + 1 < working.Count ? 2 : 1;
      for (int i = 0; i < count; i++)
        working.RemoveAt(index);
      _selected = index - 1;
    }
    else if (index > 0 && working[index - 1].Kind == OverlayPieceKind.Operator)
    {
      working.RemoveAt(index);
      working.RemoveAt(index - 1);
      _selected = index - 2;
    }
    else if (index + 1 < working.Count && working[index + 1].Kind == OverlayPieceKind.Operator)
    {
      working.RemoveAt(index + 1);
      working.RemoveAt(index);
      _selected = Math.Min(index, working.Count - 1);
    }
    else
    {
      working.RemoveAt(index);
      _selected = working.Count == 0 ? -1 : Math.Min(index, working.Count - 1);
    }
    if (_selected >= working.Count)
      _selected = working.Count - 1;
    Commit();
    return true;
  }

  // ---- What input methods set ----

  /// <summary>The picked words become <paramref name="words"/>.</summary>
  public void SetText(string words)
  {
    OverlayPiece piece = SelectedPiece;
    if (piece == null || piece.Kind != OverlayPieceKind.Text || piece.Text == (words ?? ""))
      return;
    piece.Text = words ?? "";
    Commit();
  }

  /// <summary>The picked color becomes <paramref name="color"/>: one picked by hand, or the highlight color.</summary>
  public void SetColor(OverlayPiece color)
  {
    OverlayPiece piece = SelectedPiece;
    if (piece == null || !IsColor(piece) || piece.SameAs(color))
      return;
    Working.Set(_selected, color);
    Commit();
  }

  /// <summary>The picked point becomes <paramref name="point"/>.</summary>
  public void SetPoint(OverlayGrowth point)
  {
    OverlayPiece piece = SelectedPiece;
    if (piece == null || piece.Kind != OverlayPieceKind.Point || piece.Point == point)
      return;
    piece.Point = point;
    Commit();
  }

  public void SetBool(bool value)
  {
    OverlayPiece piece = SelectedPiece;
    if (piece == null || piece.Kind != OverlayPieceKind.Bool || piece.Bool == value)
      return;
    piece.Bool = value;
    Commit();
  }

  // ---- Values ----

  /// <summary>
  /// Whether <paramref name="value"/> may be dropped on <paramref name="slot"/>
  /// at all, before it opens: a value whose type the field can hold, or one
  /// made on the spot.
  /// </summary>
  public static bool Accepts(FormulaSlot slot, OverlayValueDefinition value)
  {
    if (slot == null)
      return false;
    OverlayValueType type = OverlayVariableTypes.TypeOf(value);
    // Numbers also go into a condition's comparison, wherever it is.
    return OverlayVariableTypes.Fits(slot.Type, type) || type == OverlayValueType.Number;
  }

  /// <summary>Whether <paramref name="value"/> can go where values go now: after the picked piece, or in its place.</summary>
  public bool CanDrop(OverlayValueDefinition value)
  {
    // A gradient is a whole color of its own: only a color field itself takes one.
    if (value.Id == OverlayValueId.MakeGradient)
      return Active && _path.Count == 1 && Kind == OverlayValueType.Color;
    if (!Active)
      return false;
    OverlayPiece piece = PieceFor(value);
    OverlayValueType needed = NeededAt(_selected, Replaces());
    OverlayValueType found = OverlayVariableTypes.TypeOf(piece);
    // A comparison or a point goes only where one is needed, and only one or a condition goes there.
    bool own = needed == OverlayValueType.Compare || needed == OverlayValueType.Point;
    if (own)
      return found == needed || piece.Kind == OverlayPieceKind.Condition;
    return OverlayVariableTypes.Fits(needed, found)
      && found != OverlayValueType.Compare
      && found != OverlayValueType.Point;
  }

  /// <summary>
  /// <paramref name="value"/> goes into <paramref name="slot"/> (which the
  /// keypad then has): after the picked piece, or in its place where two
  /// values can't sit side by side. A value made on the spot becomes a new
  /// piece of its kind and is picked.
  /// </summary>
  public FormulaDropResult Drop(FormulaSlot slot, OverlayValueDefinition value)
  {
    if (!Accepts(slot, value))
      return FormulaDropResult.Refused;
    Activate(slot);
    if (!CanDrop(value))
      return FormulaDropResult.Refused;
    Put(PieceFor(value));
    Commit();
    return FormulaDropResult.Applied;
  }

  /// <summary>The piece a value becomes: a reference to a live value, or a new piece of a made kind.</summary>
  public static OverlayPiece PieceFor(OverlayValueDefinition value) =>
    value.Id switch
    {
      OverlayValueId.MakeNumber => OverlayPiece.NumberPiece("0"),
      OverlayValueId.MakeText => OverlayPiece.TextPiece(""),
      OverlayValueId.MakeBool => OverlayPiece.BoolPiece(true),
      OverlayValueId.MakeCondition => OverlayPiece.ConditionPiece(),
      OverlayValueId.MakeGroup => OverlayPiece.GroupPiece(new OverlayVariable()),
      OverlayValueId.MakeCompare => OverlayPiece.ComparePiece('≥'),
      OverlayValueId.MakePoint => OverlayPiece.PointPiece(OverlayGrowth.Auto),
      OverlayValueId.CustomColor => OverlayPiece.ColorPiece("#FFFFFF"),
      _ => OverlayPiece.ValuePiece(value.Id),
    };

  // ---- Where things go ----

  // Puts a value after the picked piece, or in its place where it can't sit beside it.
  private void Put(OverlayPiece piece)
  {
    OverlayVariable working = Working;
    if (Replaces())
    {
      int index = _selected >= 0 ? _selected : working.Count - 1;
      working.Set(index, piece);
      _selected = index;
      return;
    }
    InsertAfter(piece);
  }

  // A value replaces the picked one unless it can sit beside it (in Any) or follows an operator.
  private bool Replaces()
  {
    OverlayVariable working = Working;
    if (working == null || working.Count == 0)
      return false;
    if (Kind == OverlayValueType.Compare || Kind == OverlayValueType.Point)
      return true;
    OverlayPiece at = _selected >= 0 ? working[_selected] : working.Last;
    if (at.Kind == OverlayPieceKind.Operator)
      return false;
    if (Kind != OverlayValueType.Any)
      return true;
    // In Any, a value beside a calculation's number would join nothing: it replaces that number.
    int index = _selected >= 0 ? _selected : working.Count - 1;
    return InCalculation(working, index);
  }

  private void InsertAfter(OverlayPiece piece)
  {
    OverlayVariable working = Working;
    int at = _selected >= 0 ? _selected + 1 : working.Count;
    working.Insert(at, piece);
    _selected = at;
  }

  // What a piece put at <paramref name="index"/> (in its place, or after it) must give.
  private OverlayValueType NeededAt(int index, bool replacing)
  {
    OverlayVariable working = Working;
    OverlayValueType kind = Kind;
    if (working == null || working.Count == 0)
      return kind;
    if (index < 0)
      index = working.Count - 1;
    if (!replacing)
      return working[index].Kind == OverlayPieceKind.Operator ? OverlayValueType.Number : kind;
    return InCalculation(working, index) ? OverlayValueType.Number : kind;
  }

  private static bool InCalculation(OverlayVariable variable, int index) =>
    (index > 0 && variable[index - 1].Kind == OverlayPieceKind.Operator)
    || (index + 1 < variable.Count && variable[index + 1].Kind == OverlayPieceKind.Operator);

  private static bool Calculable(OverlayPiece piece)
  {
    OverlayValueType type = OverlayVariableTypes.TypeOf(piece);
    return type == OverlayValueType.Number || (type == OverlayValueType.Any && piece.Kind != OverlayPieceKind.Text);
  }

  private static bool IsColor(OverlayPiece piece) =>
    piece.Kind == OverlayPieceKind.Color
    || (piece.Kind == OverlayPieceKind.Value && OverlayValueCatalog.Get(piece.Value).IsColor);

  private static bool IsCompare(char key) => key is '<' or '≤' or '=' or '≠' or '≥' or '>';

  private static int Digits(string text)
  {
    int digits = 0;
    foreach (char c in text)
      if (char.IsDigit(c))
        digits++;
    return digits;
  }

  // The variable changed in place: its readers follow, and the field takes it while it fits.
  private void Commit()
  {
    _typed = true;
    _problem = Check();
    Changed();
    if (_problem == null)
      _slot.Write(_root.Clone());
  }

  // What keeps the edit from being finished: why the field's variable can't be what the field needs.
  private OverlayTypeProblem Check() =>
    _root == null ? null : OverlayVariableTypes.Check(_root, _slot.Type, _slot.IsText);
}
