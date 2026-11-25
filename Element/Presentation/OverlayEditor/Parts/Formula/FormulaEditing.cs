using System.Collections.Generic;
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
/// keeps what it had while it doesn't (<see cref="Problem"/>). A value the
/// player made opens the same way; what changes in it goes to the
/// <see cref="ICustomValueBook"/> while it gives what it was made to give.
/// The dock and its parts read it and follow each change by themselves.
/// </summary>
internal sealed class FormulaEditing : Observable
{
  private readonly ICustomValueBook _book;
  private readonly List<FormulaFrame> _path = new();
  private FormulaSlot _slot;
  private OverlayVariable _root;
  private int _selected = -1;
  private bool _typed;
  private OverlayTypeProblem _problem;

  /// <param name="book">Where made values are kept; without one, none can be made or opened.</param>
  public FormulaEditing(ICustomValueBook book = null) => _book = book;

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

  /// <summary>Whether the picked piece has a variable to go into: a condition's slot, a group's or a made value's.</summary>
  public bool CanEnter(int slot)
  {
    OverlayPiece piece = SelectedPiece;
    if (piece == null)
      return false;
    if (piece.Kind == OverlayPieceKind.Custom)
      return slot == 0 && _book != null && OverlayCustomValues.TryGet(piece.Text, out _) && !Opened(piece.Text);
    return slot >= 0 && slot < piece.Slots.Length;
  }

  /// <summary>Goes into slot <paramref name="slot"/> of the picked condition, the picked group or made value.</summary>
  public void Enter(int slot)
  {
    if (!CanEnter(slot))
      return;
    OverlayPiece piece = SelectedPiece;
    if (piece.Kind == OverlayPieceKind.Custom)
    {
      // Its own copy: the book takes it while it gives what the value was made to give.
      OverlayCustomValues.TryGet(piece.Text, out OverlayVariable made);
      OverlayVariable copy = made.Clone();
      _path.Add(
        new FormulaFrame(copy, OverlayCustomValues.TypeOf(piece.Text), 0, false, piece)
        {
          Outer = _selected,
          Custom = piece.Text,
        }
      );
      _selected = copy.Count - 1;
      _problem = Check();
      Changed();
      return;
    }
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
    Changed();
  }

  /// <summary>The picked made value is called <paramref name="name"/>, in every element.</summary>
  public void Rename(string name)
  {
    OverlayPiece piece = SelectedPiece;
    if (_book == null || piece == null || piece.Kind != OverlayPieceKind.Custom)
      return;
    _book.Rename(piece.Text, name);
    Changed();
  }

  // Whether the made value <paramref name="key"/> is open on the way here, so it can't go into itself.
  private bool Opened(string key)
  {
    foreach (FormulaFrame frame in _path)
      if (frame.Custom == key)
        return true;
    return false;
  }

  /// <summary>Back out of the innermost slot, the piece it belongs to picked again.</summary>
  public bool Back()
  {
    if (_path.Count <= 1)
      return false;
    FormulaFrame left = _path[_path.Count - 1];
    _path.RemoveAt(_path.Count - 1);
    _selected = left.Outer;
    // A made value left as it can't be keeps what it gave before.
    _problem = Check();
    Changed();
    return true;
  }

  /// <summary>Back out to depth <paramref name="depth"/> of the path (0 is the field).</summary>
  public void BackTo(int depth)
  {
    while (_path.Count > depth + 1 && Back()) { }
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
      return IsCompare(key) && !(working.Count == 1 && working[0].Op == key);
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
      return picked.Op != op || op == '-';
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
      working.Clear();
      working.Add(OverlayPiece.ComparePiece(key));
      _selected = 0;
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
    if (
      (picked == null && working.Count == 0)
      || (picked != null && picked.Kind == OverlayPieceKind.Operator && op == '-' && picked.Op != '-')
    )
    {
      // A leading minus, or one right after an operator, starts a negative number.
      InsertAfter(OverlayPiece.NumberPiece("-"));
      Commit();
      return true;
    }
    if (picked != null && picked.Kind == OverlayPieceKind.Operator)
      picked.Op = op;
    else
      InsertAfter(OverlayPiece.OperatorPiece(op));
    Commit();
    return true;
  }

  public bool CanBackspace => Active && Working.Count > 0;

  /// <summary>Removes the picked number's last digit, or the picked piece (the last one with none picked).</summary>
  public bool Backspace()
  {
    if (!CanBackspace)
      return false;
    OverlayVariable working = Working;
    int index = _selected >= 0 ? _selected : working.Count - 1;
    OverlayPiece piece = working[index];
    if (piece.Kind == OverlayPieceKind.Number && piece.Text.Length > 1)
      piece.Text = piece.Text.Substring(0, piece.Text.Length - 1);
    else
    {
      working.RemoveAt(index);
      _selected = working.Count == 0 ? -1 : System.Math.Max(0, index - 1);
    }
    Commit();
    return true;
  }

  public bool CanClear => Active && Working.Count > 0;

  /// <summary>Empties the innermost open variable.</summary>
  public void Clear()
  {
    if (!CanClear)
      return;
    Working.Clear();
    _selected = -1;
    Commit();
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
    if (value.Id == OverlayValueId.MakeCustom)
      return true;
    OverlayValueType type = OverlayVariableTypes.TypeOf(value);
    // Numbers also go into a condition's comparison, wherever it is.
    return OverlayVariableTypes.Fits(slot.Type, type) || type == OverlayValueType.Number;
  }

  /// <summary>Whether <paramref name="value"/> can go where values go now: after the picked piece, or in its place.</summary>
  public bool CanDrop(OverlayValueDefinition value)
  {
    if (value.Id == OverlayValueId.MakeCustom)
      return CanMake;
    if (!Active || Kind == OverlayValueType.Compare)
      return false;
    // A made value can't use itself.
    if (value.IsMine && Opened(value.Key.Substring(1)))
      return false;
    OverlayPiece piece = PieceFor(value);
    OverlayValueType needed = NeededAt(_selected, Replaces());
    OverlayValueType found = OverlayVariableTypes.TypeOf(piece);
    return OverlayVariableTypes.Fits(needed, found) && found != OverlayValueType.Compare;
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
    if (value.Id == OverlayValueId.MakeCustom)
      return Make() ? FormulaDropResult.Applied : FormulaDropResult.Refused;
    if (!CanDrop(value))
      return FormulaDropResult.Refused;
    Put(PieceFor(value));
    Commit();
    return FormulaDropResult.Applied;
  }

  /// <summary>Whether a new value of the player's can be made where values go now.</summary>
  public bool CanMake =>
    Active && _book != null && Kind != OverlayValueType.Compare && Working.Count < OverlayVariable.MaxPieces;

  /// <summary>
  /// Makes a new value of the player's that gives what is needed where it
  /// goes, puts it there and opens it, to be filled like any variable.
  /// </summary>
  public bool Make()
  {
    if (!CanMake)
      return false;
    string key = _book.Make(NeededAt(_selected, Replaces()));
    Put(OverlayPiece.CustomPiece(key));
    Commit();
    Enter(0);
    return true;
  }

  /// <summary>The piece a value becomes: a reference to a live or made value, or a new piece of a made kind.</summary>
  public static OverlayPiece PieceFor(OverlayValueDefinition value) =>
    value.Id switch
    {
      OverlayValueId.MakeNumber => OverlayPiece.NumberPiece("0"),
      OverlayValueId.MakeText => OverlayPiece.TextPiece(""),
      OverlayValueId.MakeBool => OverlayPiece.BoolPiece(true),
      OverlayValueId.MakeCondition => OverlayPiece.ConditionPiece(),
      OverlayValueId.MakeGroup => OverlayPiece.GroupPiece(new OverlayVariable()),
      OverlayValueId.CustomColor => OverlayPiece.ColorPiece("#FFFFFF"),
      _ when value.IsMine => OverlayPiece.CustomPiece(value.Key.Substring(1)),
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
    if (Kind == OverlayValueType.Compare)
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

  // The variable changed in place: its readers follow, the made values open
  // on the way take theirs while they fit, and the field takes it while it fits.
  private void Commit()
  {
    _typed = true;
    for (int i = _path.Count - 1; i >= 1; i--)
    {
      FormulaFrame frame = _path[i];
      if (frame.Custom != null && OverlayVariableTypes.Check(frame.Variable, frame.Type) == null)
        _book.Write(frame.Custom, frame.Variable);
    }
    _problem = Check();
    Changed();
    if (_problem == null)
      _slot.Write(_root.Clone());
  }

  // What keeps the edit from being finished: the innermost open made value
  // that can't be what it was made to give, then the field itself.
  private OverlayTypeProblem Check()
  {
    for (int i = _path.Count - 1; i >= 1; i--)
    {
      FormulaFrame frame = _path[i];
      if (frame.Custom == null)
        continue;
      OverlayTypeProblem problem = OverlayVariableTypes.Check(frame.Variable, frame.Type);
      if (problem != null)
        return problem;
    }
    return _root == null ? null : OverlayVariableTypes.Check(_root, _slot.Type, _slot.IsText);
  }
}
