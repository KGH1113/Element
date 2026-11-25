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

  /// <summary>Whether a field is open; with none the line says <see cref="Hint"/>.</summary>
  public bool Open;

  /// <summary>What the line says with nothing open.</summary>
  public string Hint = "";

  /// <summary>The open field's name: "너비", "크기".</summary>
  public string Name = "";

  /// <summary>What the open field gives, for its mark.</summary>
  public OverlayValueType Type;

  /// <summary>What it works out to, with its unit: "= 192 px"; empty when there is nothing to work out.</summary>
  public string Result = "";

  public readonly LineSlotViewModel Root = new("s");

  /// <summary>A piece or a slot clicked: it is picked.</summary>
  public void Pick(FormulaStep[] route, int index) => _actions.SelectAt(route, index);

  /// <summary>
  /// The line for what <paramref name="editing"/> has open, called
  /// <paramref name="name"/>, working out to <paramref name="result"/>; with
  /// nothing open, <paramref name="hint"/>.
  /// </summary>
  public void Show(FormulaEditing editing, string name, string result, string hint)
  {
    Open = editing != null && editing.Active;
    Hint = hint ?? "";
    if (!Open)
    {
      Result = "";
      return;
    }
    // In a value of the player's the line shows its inside, under its name.
    int lineBase = editing.LineBase;
    FormulaFrame start = editing.Path[lineBase];
    bool made = lineBase > 0;
    Name = made ? OverlayCustomValues.Shown(start.Custom, FormulaWords.Current) : name ?? "";
    Type = start.Type;
    Result = made || string.IsNullOrEmpty(result) || IsPlain(start.Variable) ? "" : "= " + result;
    SelectedRoute(editing, lineBase);
    _used.Clear();
    _route.Clear();
    Slot(start.Variable, start.Type, -1, !made && editing.Slot.IsText, true, editing);
    Forget(_pieces);
    Forget(_slots);
  }

  // A plain number or a single value reads the same as its result.
  private static bool IsPlain(OverlayVariable variable) =>
    variable.Count == 1 && variable[0].Kind is not (OverlayPieceKind.Condition or OverlayPieceKind.Group);

  // Where the picked piece is: the route of the innermost open variable.
  private void SelectedRoute(FormulaEditing editing, int lineBase)
  {
    _selectedRoute.Clear();
    IReadOnlyList<FormulaFrame> path = editing.Path;
    for (int i = lineBase + 1; i < path.Count; i++)
      _selectedRoute.Add(new FormulaStep(path[i].Outer, path[i].Slot));
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
    string key = "s" + RouteKey(_route);
    if (!_slots.TryGetValue(key, out LineSlotViewModel slot))
      _slots[key] = slot = new LineSlotViewModel(key);
    _used.Add(key);
    slot.Route = _route.ToArray();
    slot.Type = type;
    slot.Root = root;
    bool here = SameRoute(_route, _selectedRoute);
    slot.Targeted = here && editing.Selected < 0;
    slot.Missing = variable.IsEmpty && !mayBeEmpty;
    slot.Placeholder =
      root ? L(FormulaRefusals.TypeName(type) + " 넣기", "Add " + FormulaRefusals.TypeName(type).ToLowerInvariant())
      : slotIndex >= 0
        ? FormulaRefusals.SlotName(slotIndex) + (mayBeEmpty ? L(" (비워도 돼요)", " (may stay empty)") : "")
      : FormulaRefusals.TypeName(type);
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
    LinePieceKind kind = piece.Kind switch
    {
      OverlayPieceKind.Operator => LinePieceKind.Operator,
      OverlayPieceKind.Condition => LinePieceKind.Condition,
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
    if (kind == LinePieceKind.Condition)
    {
      vm.Slots = new LineSlotViewModel[5];
      for (int slot = 0; slot < 5; slot++)
      {
        _route.Add(new FormulaStep(index, slot));
        OverlayValueType type =
          slot == OverlayPiece.Compare ? OverlayValueType.Compare
          : slot == OverlayPiece.Left || slot == OverlayPiece.Right ? OverlayValueType.Number
          : gives;
        bool mayBeEmpty = slot >= OverlayPiece.Result && gives == OverlayValueType.Any;
        vm.Slots[slot] = Slot(piece.Slots[slot], type, slot, mayBeEmpty, false, editing);
        _route.RemoveAt(_route.Count - 1);
      }
      OverlayVariable compare = piece.Slots[OverlayPiece.Compare];
      vm.Compare = compare.Count > 0 ? OverlayVariable.Symbol(compare[0].Op).ToString() : "?";
      LineSlotViewModel compareSlot = vm.Slots[OverlayPiece.Compare];
      vm.CompareSelected = compareSlot.Targeted || (compareSlot.Pieces.Length > 0 && compareSlot.Pieces[0].Selected);
    }
    else if (kind == LinePieceKind.Group)
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
      case OverlayPieceKind.Custom:
        return OverlayCustomValues.Shown(piece.Text, words);
      case OverlayPieceKind.Condition:
        return words.ConditionStart;
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
    foreach (LinePieceViewModel piece in _pieces.Values)
      piece.Dispose();
    foreach (LineSlotViewModel slot in _slots.Values)
      slot.Dispose();
    _pieces.Clear();
    _slots.Clear();
  }
}
