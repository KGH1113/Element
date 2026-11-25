using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ShapeEditor;

/// <summary>
/// Where a value carried out of the dock can land in the shape editor, each
/// number answering for itself: W, H, a corner's rounding (its number or its
/// arc), the border width row, the picker's opacity, a color's position or
/// its square, and the handles' angle. Letting go calculates that number with
/// the value; words such as the song title are refused, with a reason.
/// ElementUI finds the place under the pointer, so whatever is drawn over a
/// place shields it. The dock's formula line is the dock's own place.
/// </summary>
internal sealed class ShapeValueDrops
{
  private readonly IShapeEditorActions _editor;
  private readonly ShapeNumbers _numbers;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly ShapeMeasures _measures;
  private readonly List<IDisposable> _places = new();

  public ShapeValueDrops(
    IShapeEditorActions editor,
    ShapeNumbers numbers,
    ShapeColors colors,
    ShapeGradient gradient,
    ShapeMeasures measures
  )
  {
    _editor = editor;
    _numbers = numbers;
    _colors = colors;
    _gradient = gradient;
    _measures = measures;
  }

  /// <summary>
  /// Once the editor's numbers, picker and handles are built: each of them
  /// takes values from now on. The keys of the picker's numbers are read when
  /// a value lands, since the picker shows the fill's color or the border's.
  /// </summary>
  public void Build()
  {
    Release();
    _places.Add(Number(_measures.WidthField, () => ShapeMeasures.WidthKey));
    _places.Add(Number(_measures.HeightField, () => ShapeMeasures.HeightKey));
    for (int i = 0; i < _measures.RadiusFields.Count; i++)
    {
      int corner = i;
      _places.Add(Number(_measures.RadiusFields[i], () => _measures.RadiusKey(corner)));
      _places.Add(
        UguiDrop.Destination<OverlayValueDefinition>(
          _measures.Arcs[i].gameObject,
          over: Numeric,
          drop: value => DropOn(_measures.RadiusKey(corner), value),
          refused: value => DropOn(_measures.RadiusKey(corner), value)
        )
      );
    }
    _places.Add(Number(_colors.Opacity.Field, () => _colors.OpacityKey));
    _places.Add(Number(_gradient.Position, () => _gradient.PositionKey));
    _places.Add(Number(_gradient.Handles.Angle, () => _gradient.AngleKey));
    _places.Add(UguiDrop.Destination<OverlayValueDefinition>(_gradient.Handles.Root.gameObject, new Squares(this)));
  }

  /// <summary>The editor closes or is laid out afresh: its places go with what they were on.</summary>
  public void Release()
  {
    foreach (IDisposable place in _places)
      place.Dispose();
    _places.Clear();
  }

  /// <summary>A value let go on the border width row: it calculates the width with the value, or says why words can't.</summary>
  public void DropOnStrokeWidth(OverlayValueDefinition value) => DropOn(ShapeNumbers.StrokeKey, value);

  private IDisposable Number(FormulaFieldView field, Func<string> key) =>
    UguiDrop.Destination<OverlayValueDefinition>(
      field.Root.gameObject,
      over: Numeric,
      drop: value => DropOn(key(), value),
      targeted: field.SetDrop,
      refused: value => DropOn(key(), value)
    );

  private static DropState Numeric(OverlayValueDefinition value) =>
    value.IsNumeric ? DropState.Accepts : DropState.Refuses;

  // Let go on a number: it opens in the dock and is calculated with the value;
  // words can't be, and it says so.
  private void DropOn(string key, OverlayValueDefinition value)
  {
    if (!value.IsNumeric)
      _numbers.Refuse(value);
    else
    {
      if (_numbers.Field != key)
      {
        _editor.CloseOpen();
        _numbers.Field = key;
      }
      FormulaEditing editing = _numbers.Editing;
      editing.Activate(_numbers.Slot(key));
      editing.Drop(editing.Slot, value);
    }
    _editor.Render();
  }

  // A gradient's square takes a value for its color's position; the stop it
  // belongs to is picked first.
  private sealed class Squares : IDropDestination<OverlayValueDefinition>
  {
    private readonly ShapeValueDrops _drops;
    private int _stop = -1;

    public Squares(ShapeValueDrops drops) => _drops = drops;

    public DropState Over(OverlayValueDefinition value, Vector2 screen)
    {
      _stop = _drops._gradient.Handles.SquareAt(screen);
      return _stop < 0 ? DropState.None : Numeric(value);
    }

    public void Drop(OverlayValueDefinition value, Vector2 screen)
    {
      _drops._colors.Picker.Gradient.SelectStop(_stop);
      _drops.DropOn(_drops._gradient.PositionKey, value);
    }

    public void Refused(OverlayValueDefinition value, Vector2 screen) => Drop(value, screen);

    public void Left() => _stop = -1;
  }
}
