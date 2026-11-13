using Element.Domain.Overlay.Values;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.Popovers;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// A value dragged from the dock onto the shape editor: the number under it
/// lights up when it can take it, and dropping it there calculates that
/// number with the value. Words such as the song title are refused.
/// </summary>
internal sealed class ShapeValueDrops
{
  private const string FormulaLine = "formula";

  private readonly IShapeEditorHost _host;
  private readonly ShapeNumbers _numbers;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly ShapeMeasures _measures;
  private readonly ShapeFieldList _fields;
  private OverlayValueDefinition _dragValue;
  private string _dropKey;

  public ShapeValueDrops(
    IShapeEditorHost host,
    ShapeNumbers numbers,
    ShapeColors colors,
    ShapeGradient gradient,
    ShapeMeasures measures,
    ShapeFieldList fields
  )
  {
    _host = host;
    _numbers = numbers;
    _colors = colors;
    _gradient = gradient;
    _measures = measures;
    _fields = fields;
  }

  /// <summary>A value is being carried.</summary>
  public bool Dragging => _dragValue != null;

  /// <summary>Drops what was carried, as when the editor is laid out afresh or closed.</summary>
  public void Cancel() => _dragValue = null;

  public void BeginValueDrag(OverlayValueDefinition value, Vector2 screen)
  {
    _dragValue = value;
    MoveValueDrag(screen);
  }

  public void MoveValueDrag(Vector2 screen)
  {
    OverlayEditorFrame frame = _host.Screen;
    if (_dragValue == null || frame == null)
      return;
    _dropKey = FindDrop(screen, out bool accepted);
    _fields.Collect();
    for (int i = 0; i < _fields.Count; i++)
      _fields[i].SetDrop(_fields.Key(i) == _dropKey ? (accepted ? 1 : -1) : 0);
    _numbers.StrokeWidth.SetDrop(_dropKey == ShapeNumbers.StrokeKey ? (accepted ? 1 : -1) : 0);
    _host.Dock.SetDrop(_dropKey == FormulaLine ? (accepted ? 1 : -1) : 0);
    frame.ShowGhost(ValueSectionNames.ValueName(_dragValue), screen, accepted || _dropKey == null ? 1 : -1);
  }

  public void EndValueDrag(Vector2 screen)
  {
    OverlayEditorFrame frame = _host.Screen;
    if (_dragValue == null || frame == null)
      return;
    OverlayValueDefinition value = _dragValue;
    string key = FindDrop(screen, out _);
    _dragValue = null;
    _dropKey = null;
    _fields.Collect();
    for (int i = 0; i < _fields.Count; i++)
      _fields[i].SetDrop(0);
    _numbers.StrokeWidth.SetDrop(0);
    _host.Dock.SetDrop(0);
    frame.HideGhost();
    OverlayFormulaEditing editing = _numbers.Editing;
    if (key == FormulaLine)
    {
      if (editing.Active && editing.Drop(editing.Slot, value) == OverlayDropResult.RefusedWords)
        _numbers.RefuseWords(value);
    }
    else if (key != null)
    {
      // A value dropped on a gradient square drives that color's position.
      if (ShapeNumbers.Kind(key, out int stop).EndsWith("position") && stop >= 0)
      {
        _colors.Picker.Gradient.SelectStop(stop);
        key = _gradient.PositionKey;
      }
      if (!value.IsNumeric)
        _numbers.RefuseWords(value);
      else
      {
        if (_numbers.Field != key)
        {
          _host.CloseOpen();
          _numbers.Field = key;
        }
        editing.Activate(_numbers.Slot(key));
        editing.Drop(editing.Slot, value);
      }
    }
    _host.Render();
  }

  private string FindDrop(Vector2 screen, out bool accepted)
  {
    accepted = _dragValue.IsNumeric;
    // The picker sits over everything else; only its numbers take a value.
    if (_colors.Picker.Visible && PopoverPlacement.Contains(_colors.Picker.Root, screen))
    {
      if (_colors.Opacity.Contains(screen))
        return _colors.OpacityKey;
      if (_colors.Picker.Gradient.IsGradient && _gradient.Position.Contains(screen))
        return _gradient.PositionKey;
      accepted = true;
      return null;
    }
    OverlayGradientHandles handles = _gradient.Handles;
    if (handles.Visible)
    {
      if (handles.Angle.Contains(screen))
        return _gradient.AngleKey;
      int square = handles.SquareAt(screen);
      if (square >= 0)
        return _gradient.PositionKey + ":" + square;
    }
    OverlayInputDock dock = _host.Dock;
    if (dock.FormulaContains(screen))
    {
      accepted = _numbers.Editing.Active && OverlayFormulaEditing.Accepts(_numbers.Editing.Slot, _dragValue);
      return FormulaLine;
    }
    if (dock.Contains(screen))
    {
      accepted = true;
      return null;
    }
    if (_host.Inspector.Contains(_numbers.StrokeWidth, screen))
      return ShapeNumbers.StrokeKey;
    _fields.Collect();
    for (int i = 0; i < _fields.Count; i++)
      if (_fields[i].Root.gameObject.activeInHierarchy && _fields[i].Contains(screen))
        return _fields.Key(i);
    int arc = _measures.ArcAt(screen, true);
    if (arc >= 0)
      return _measures.RadiusKey(arc);
    accepted = true;
    return null;
  }
}
