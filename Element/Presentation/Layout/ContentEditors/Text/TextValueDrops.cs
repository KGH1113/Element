using Element.Domain.Elements;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// A value dragged from the dock into the text editor: into the text at an
/// accent caret, onto a number of the bar, the picker or the handles to
/// calculate it, onto the dock's formula line, or onto the value whose
/// formula waits for its next operand. What can't take it says why.
/// </summary>
internal sealed class TextValueDrops
{
  private enum DropTarget
  {
    None,
    Size,
    Opacity,
    Position,
    Angle,
    Formula,
    Join,
    Insert,
  }

  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextLayoutMap _map;
  private readonly TextMarks _marks;
  private readonly TextSize _size;
  private readonly TextColor _color;
  private readonly TextGradient _gradient;
  private OverlayValueDefinition _dragValue;
  private DropTarget _dropTarget;
  private int _dropIndex;

  public TextValueDrops(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextLayoutMap map,
    TextMarks marks,
    TextSize size,
    TextColor color,
    TextGradient gradient
  )
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    _map = map;
    _marks = marks;
    _size = size;
    _color = color;
    _gradient = gradient;
  }

  /// <summary>A value is being carried.</summary>
  public bool Dragging => _dragValue != null;

  /// <summary>Where a carried value would be inserted in the text; -1 when it isn't over the text.</summary>
  public int InsertIndex => _dragValue != null && _dropTarget == DropTarget.Insert ? _dropIndex : -1;

  /// <summary>A carried value is over the value whose formula waits for it.</summary>
  public bool Joining => _dragValue != null && _dropTarget == DropTarget.Join;

  /// <summary>Drops what was carried, as when the editor is laid out afresh or closed.</summary>
  public void Cancel() => _dragValue = null;

  public void BeginValueDrag(OverlayValueDefinition value, Vector2 screen)
  {
    _dragValue = value;
    _host.Screen.HideTip();
    MoveValueDrag(screen);
  }

  public void MoveValueDrag(Vector2 screen)
  {
    OverlayEditorFrame frame = _host.Screen;
    if (_dragValue == null || frame == null)
      return;
    _dropTarget = FindDrop(screen, out _dropIndex, out bool accepted);
    _size.Button.SetDrop(_dropTarget == DropTarget.Size ? (accepted ? 1 : -1) : 0);
    _color.Opacity.Field.SetDrop(_dropTarget == DropTarget.Opacity ? (accepted ? 1 : -1) : 0);
    _gradient.Position.SetDrop(_dropTarget == DropTarget.Position && _dropIndex < 0 ? (accepted ? 1 : -1) : 0);
    _gradient.Handles.Angle.SetDrop(_dropTarget == DropTarget.Angle ? (accepted ? 1 : -1) : 0);
    _host.Dock.SetDrop(_dropTarget == DropTarget.Formula ? (accepted ? 1 : -1) : 0);
    frame.ShowGhost(OverlayKeypad.ValueName(_dragValue), screen, accepted || _dropTarget == DropTarget.None ? 1 : -1);
    _marks.Place(InsertIndex, Joining, _formulas.EditedValue);
  }

  public void EndValueDrag(Vector2 screen)
  {
    OverlayEditorFrame frame = _host.Screen;
    if (_dragValue == null || frame == null)
      return;
    OverlayValueDefinition value = _dragValue;
    DropTarget target = FindDrop(screen, out int index, out _);
    _dragValue = null;
    _dropTarget = DropTarget.None;
    _size.Button.SetDrop(0);
    _color.Opacity.Field.SetDrop(0);
    _gradient.Position.SetDrop(0);
    _gradient.Handles.Angle.SetDrop(0);
    _host.Dock.SetDrop(0);
    frame.HideGhost();
    OverlayFormulaEditing editing = _formulas.Editing;
    switch (target)
    {
      case DropTarget.Position:
        // Onto a square: that color's position.
        if (index >= 0)
          _color.Picker.SelectStop(index);
        _formulas.DropOn(TextFormulas.PositionKey, value);
        break;
      case DropTarget.Angle:
        _formulas.DropOn(TextFormulas.AngleKey, value);
        break;
      case DropTarget.Size:
        _formulas.DropOn(TextFormulas.SizeKey, value);
        break;
      case DropTarget.Opacity:
        _formulas.DropOn(TextFormulas.OpacityKey, value);
        break;
      case DropTarget.Formula:
        if (editing.Active && editing.Drop(editing.Slot, value) == OverlayDropResult.RefusedWords)
          _formulas.RefuseWords(value);
        break;
      case DropTarget.Join:
        editing.Drop(_formulas.ChipSlot(index), value);
        break;
      case DropTarget.Insert:
        // A value dropped into the text lands there; typing goes on after it.
        _host.ClosePopovers();
        _text.InsertValue(value, index, index);
        _text.Anchor = _text.Caret;
        break;
    }
    _host.Render();
  }

  private DropTarget FindDrop(Vector2 screen, out int index, out bool accepted)
  {
    index = -1;
    accepted = true;
    SettingsColorPicker picker = _color.Picker;
    // The picker sits over everything else; only its numbers take a value.
    if (picker.Visible && PopoverPlacement.Contains(picker.Root, screen))
    {
      bool opacity = _color.Opacity.Contains(screen),
        position = picker.IsGradient && _gradient.Position.Contains(screen);
      accepted = !(opacity || position) || _dragValue.IsNumeric;
      return opacity ? DropTarget.Opacity
        : position ? DropTarget.Position
        : DropTarget.None;
    }
    OverlayGradientHandles handles = _gradient.Handles;
    if (handles.Visible)
    {
      if (handles.Angle.Contains(screen))
      {
        accepted = _dragValue.IsNumeric;
        return DropTarget.Angle;
      }
      int square = handles.SquareAt(screen);
      if (square >= 0)
      {
        index = square;
        accepted = _dragValue.IsNumeric;
        return DropTarget.Position;
      }
    }
    if (_size.Button.Contains(screen))
    {
      accepted = _dragValue.IsNumeric;
      return DropTarget.Size;
    }
    OverlayFormulaEditing editing = _formulas.Editing;
    OverlayInputDock dock = _host.Dock;
    if (dock.FormulaContains(screen))
    {
      accepted = editing.Active && OverlayFormulaEditing.Accepts(editing.Slot, _dragValue);
      return DropTarget.Formula;
    }
    if (dock.Contains(screen) || !_host.InTextArea(screen))
      return DropTarget.None;
    // Onto the value whose formula waits for its next number or value.
    if (
      editing.Active
      && editing.Working.AwaitsOperand
      && _formulas.ChipOpen(out int selected)
      && _map.ItemContains(selected, screen)
    )
    {
      index = selected;
      accepted = _dragValue.IsNumeric;
      return DropTarget.Join;
    }
    index = _map.IndexAt(screen);
    return DropTarget.Insert;
  }
}
