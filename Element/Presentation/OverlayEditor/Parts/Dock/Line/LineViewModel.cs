using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Line;

/// <summary>
/// The dock's line: what is open (its mark and name) and every piece of it
/// laid out in place, a condition's slots in a row, so any piece or slot is
/// one click away; the result on the right. With nothing open it says what to
/// do. Rebuilt from the open variable each time the editor shows the dock;
/// pieces and slots keep their objects while their place stays, so the views
/// on them stay too.
/// </summary>
internal sealed class LineViewModel : ViewModel
{
  private readonly IInputDockActions _actions;
  private readonly Dictionary<string, LinePieceViewModel> _pieces = new();
  private readonly Dictionary<string, LineSlotViewModel> _slots = new();
  private readonly HashSet<string> _used = new();
  private readonly List<FormulaStep> _route = new();
  private readonly List<FormulaStep> _selectedRoute = new();

  public LineViewModel(IInputDockActions actions)
  {
    _actions = actions;
    // The field's own variable keeps one object, so the line's views on it stay.
    _slots[Root.Key] = Root;
  }

  /// <summary>Whether a field is open; with none the line is clear.</summary>
  public bool Open;

  /// <summary>Apply was refused: what is missing on the line is outlined until it is put right.</summary>
  public bool Alarmed;

  /// <summary>Shakes the line when what was asked can't be done.</summary>
  public readonly EditorShake Shake = new();

  /// <summary>The open field's name: "너비", "크기".</summary>
  public string Name = "";

  /// <summary>Whose field it is, said before its name: "글 전체 ›", "“FPS” ›"; empty for a field of the element itself.</summary>
  public string Whose = "";

  /// <summary>
  /// The field is picked from a fixed set (a font, a point), not made of
  /// values: the line shows its one choice, and the input method's place has
  /// the set.
  /// </summary>
  public bool Choosing;

  /// <summary>What a <see cref="Choosing"/> field's mark says ("Aa", a place).</summary>
  public string ChoiceGlyph = "";

  /// <summary>What a <see cref="Choosing"/> field holds now: "기본", "자동".</summary>
  public string ChoiceLabel = "";

  /// <summary>What the open field gives, for its mark.</summary>
  public OverlayValueType Type;

  /// <summary>What it works out to, with its unit: "= 192 px"; empty when there is nothing to work out.</summary>
  public string Result = "";

  /// <summary>Whether the result is a color, shown as its swatch before it.</summary>
  public bool ResultHasSwatch;

  public Color ResultSwatch = Color.white;

  public readonly LineSlotViewModel Root = new("s");

  /// <summary>A piece or a slot clicked: it is picked.</summary>
  public void Pick(FormulaStep[] route, int index) => _actions.SelectAt(route, index);

  /// <summary>
  /// The line for what <paramref name="editing"/> has open, called
  /// <paramref name="name"/>, working out to <paramref name="result"/>; clear
  /// with nothing open.
  /// </summary>
  public void Show(FormulaEditing editing, string name, string result, string whose = null)
  {
    Open = editing != null && editing.Active;
    Choosing = false;
    if (!Open)
    {
      Whose = "";
      Result = "";
      return;
    }
    // The line holds one variable: the field's, or the slot of a condition
    // the dock went into. A group inside it is laid out in place.
    IReadOnlyList<FormulaFrame> path = editing.Path;
    int held = 0;
    for (int i = path.Count - 1; i > 0 && held == 0; i--)
      if (path[i].Owner?.Kind == OverlayPieceKind.Condition)
        held = i;
    FormulaFrame start = path[held];
    Name = held == 0 ? name ?? "" : FormulaRefusals.SlotName(start.Slot);
    Whose = Trail(whose, name, path, held);
    Type = start.Type;
    // A field always says what it gives; inside a condition the form beside the line shows the whole.
    Result = held > 0 || string.IsNullOrEmpty(result) ? "" : "= " + result;
    ResultHasSwatch =
      Result.Length > 0 && ColorUtility.TryParseHtmlString(result, out ResultSwatch) && result[0] == '#';
    _selectedRoute.Clear();
    for (int i = 1; i < path.Count; i++)
      _selectedRoute.Add(new FormulaStep(path[i].Outer, path[i].Slot));
    _used.Clear();
    _route.Clear();
    for (int i = 1; i <= held; i++)
      _route.Add(new FormulaStep(path[i].Outer, path[i].Slot));
    Slot(start.Variable, start.Type, -1, held == 0 ? editing.Slot.IsText : start.MayBeEmpty, true, editing);
    Forget(_pieces);
    Forget(_slots);
  }

  // The way to the variable on the line, said before its name: whose field
  // it is, then each condition gone into and the slot of it.
  private static string Trail(string whose, string name, IReadOnlyList<FormulaFrame> path, int held)
  {
    if (held == 0)
      return whose ?? "";
    System.Text.StringBuilder trail = new(whose ?? "");
    if (trail.Length > 0)
      trail.Append(' ');
    trail.Append(name).Append(" ›");
    for (int i = 1; i <= held; i++)
    {
      if (path[i].Owner?.Kind != OverlayPieceKind.Condition)
        continue;
      trail.Append(' ').Append(L("조건", "Condition")).Append(" ›");
      if (i < held)
        trail.Append(' ').Append(FormulaRefusals.SlotName(path[i].Slot)).Append(" ›");
    }
    return trail.ToString();
  }

  /// <summary>
  /// The line for a field picked from a fixed set: <paramref name="whose"/>
  /// <paramref name="name"/>, marked <paramref name="glyph"/>, holding
  /// <paramref name="label"/>.
  /// </summary>
  public void ShowChoice(string whose, string name, string glyph, string label)
  {
    Open = true;
    Choosing = true;
    Whose = whose ?? "";
    Name = name ?? "";
    ChoiceGlyph = glyph ?? "";
    ChoiceLabel = label ?? "";
    Result = "";
    ResultHasSwatch = false;
  }

  private LineSlotViewModel Slot(
    OverlayVariable variable,
    OverlayValueType type,
    int slotIndex,
    bool mayBeEmpty,
    bool root,
    FormulaEditing editing
  )
  {
    // The line's own variable keeps one object, whichever variable it is now, so the view on it stays.
    string key = root ? Root.Key : "s" + RouteKey(_route);
    if (!_slots.TryGetValue(key, out LineSlotViewModel slot))
      _slots[key] = slot = new LineSlotViewModel(key);
    _used.Add(key);
    slot.Route = _route.ToArray();
    slot.Type = type;
    slot.Root = root;
    bool here = SameRoute(_route, _selectedRoute);
    slot.Targeted = here && editing.Selected < 0;
    slot.Missing = variable.IsEmpty && !mayBeEmpty;
    slot.Placeholder = FormulaRefusals.TypeName(type);
    LinePieceViewModel[] pieces = new LinePieceViewModel[variable.Count];
    for (int i = 0; i < variable.Count; i++)
      pieces[i] = Piece(variable, i, type, here && editing.Selected == i, editing);
    slot.Pieces = pieces;
    return slot;
  }

  private LinePieceViewModel Piece(
    OverlayVariable variable,
    int index,
    OverlayValueType around,
    bool selected,
    FormulaEditing editing
  )
  {
    OverlayPiece piece = variable[index];
    // A condition is one value on the line; its five slots are its input method's.
    LinePieceKind kind = piece.Kind switch
    {
      OverlayPieceKind.Operator => LinePieceKind.Operator,
      OverlayPieceKind.Group => LinePieceKind.Group,
      _ => LinePieceKind.Chip,
    };
    string key = "p" + RouteKey(_route) + "/" + index + ":" + (int)kind;
    if (!_pieces.TryGetValue(key, out LinePieceViewModel vm))
      _pieces[key] = vm = new LinePieceViewModel(key, kind);
    _used.Add(key);
    vm.Route = _route.ToArray();
    vm.Index = index;
    vm.Selected = selected;
    vm.Type = OverlayVariableTypes.TypeOf(piece);
    vm.HasSwatch = false;
    vm.Label = Label(piece);
    if (piece.Kind == OverlayPieceKind.Color)
    {
      vm.HasSwatch = true;
      vm.Swatch = OverlayTextComposer.ParseHex(piece.Text);
    }
    else if (piece.Kind == OverlayPieceKind.Value && OverlayValueCatalog.Get(piece.Value).IsColor)
    {
      vm.HasSwatch = true;
      vm.Swatch = UiPalette.Accent;
    }
    // Inside a calculation a condition or a group gives a number.
    bool inCalculation =
      (index > 0 && variable[index - 1].Kind == OverlayPieceKind.Operator)
      || (index + 1 < variable.Count && variable[index + 1].Kind == OverlayPieceKind.Operator);
    OverlayValueType gives = inCalculation ? OverlayValueType.Number : around;
    // A condition wears the mark of what it gives where it sits.
    if (piece.Kind == OverlayPieceKind.Condition)
      vm.Type = gives;
    if (kind == LinePieceKind.Group)
    {
      _route.Add(new FormulaStep(index, 0));
      vm.Slots = new[] { Slot(piece.Slots[0], gives, -1, false, false, editing) };
      _route.RemoveAt(_route.Count - 1);
    }
    else
      vm.Slots = Array.Empty<LineSlotViewModel>();
    return vm;
  }

  /// <summary>What a piece says on the line: a number as typed, words in quotes, a value's name, a sign.</summary>
  public static string Label(OverlayPiece piece)
  {
    OverlayVariableWords words = FormulaWords.Current;
    switch (piece.Kind)
    {
      case OverlayPieceKind.Number:
        return piece.Text.Replace('-', '−');
      case OverlayPieceKind.Text:
        return "“" + piece.Text + "”";
      case OverlayPieceKind.Color:
        return piece.Text;
      case OverlayPieceKind.Bool:
        return piece.Bool ? words.Yes : words.No;
      case OverlayPieceKind.Compare:
      case OverlayPieceKind.Operator:
        return OverlayVariable.Symbol(piece.Op).ToString();
      case OverlayPieceKind.Value:
        return ValueSectionNames.ValueName(OverlayValueCatalog.Get(piece.Value));
      case OverlayPieceKind.Point:
        return words.Point(piece.Point);
      case OverlayPieceKind.Condition:
        return L("조건", "Condition");
      default:
        return "";
    }
  }

  private static string RouteKey(List<FormulaStep> route)
  {
    if (route.Count == 0)
      return "";
    System.Text.StringBuilder key = new();
    foreach (FormulaStep step in route)
      key.Append('.').Append(step.Piece).Append(',').Append(step.Slot);
    return key.ToString();
  }

  private static bool SameRoute(List<FormulaStep> a, List<FormulaStep> b)
  {
    if (a.Count != b.Count)
      return false;
    for (int i = 0; i < a.Count; i++)
      if (a[i].Piece != b[i].Piece || a[i].Slot != b[i].Slot)
        return false;
    return true;
  }

  // Lets go of the pieces and slots no longer on the line.
  private void Forget<T>(Dictionary<string, T> cache)
    where T : ViewModel
  {
    List<string> gone = null;
    foreach (KeyValuePair<string, T> entry in cache)
      if (!_used.Contains(entry.Key))
        (gone ??= new List<string>()).Add(entry.Key);
    if (gone == null)
      return;
    foreach (string key in gone)
    {
      cache[key].Dispose();
      cache.Remove(key);
    }
  }

  protected override void OnDisposed()
  {
    Shake.Dispose();
    foreach (LinePieceViewModel piece in _pieces.Values)
      piece.Dispose();
    foreach (LineSlotViewModel slot in _slots.Values)
      slot.Dispose();
    _pieces.Clear();
    _slots.Clear();
  }
}
