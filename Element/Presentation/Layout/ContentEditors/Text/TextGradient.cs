using System;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Widgets;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// Gradients in text: while the picker shows a gradient, its handles sit
/// over the text's box, where the preview spreads gradient runs as the game
/// does. The picked color's position, its opacity and the angle are numbers
/// the dock types, like the size.
/// </summary>
internal sealed class TextGradient
{
  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextPreview _preview;
  private readonly OverlayGradientEditing _gradient;
  private readonly OverlayFormulaSlot _positionSlot;
  private readonly OverlayFormulaSlot _angleSlot;
  private readonly Func<OverlayFormula> _readPosition;
  private readonly Func<OverlayFormula> _readAngle;

  // The range's color as the handles move it; written to the range on each move.
  private OverlayColorSettings _handleColor;

  public TextGradient(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextPreview preview,
    OverlayValues values
  )
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    _preview = preview;
    _gradient = new OverlayGradientEditing(values, text.RangeColor);
    _readPosition = () => ReadStop(true);
    _readAngle = ReadAngle;
    _positionSlot = new OverlayFormulaSlot(
      TextFormulas.PositionKey,
      false,
      _readPosition,
      formula => WriteStop(true, formula)
    );
    _angleSlot = new OverlayFormulaSlot(TextFormulas.AngleKey, false, _readAngle, WriteAngle);
  }

  public OverlayGradientHandles Handles => _gradient.Handles;

  public OverlayFormulaField Position => _gradient.Position;

  /// <summary>Numbers that belong to the gradient: the picked color's position and the angle.</summary>
  public static bool IsKey(string key) => key == TextFormulas.PositionKey || key == TextFormulas.AngleKey;

  /// <summary>The picked color of <paramref name="color"/>'s gradient; null without one.</summary>
  public OverlayGradientStop PickedStop(OverlayColorSettings color) => _gradient.PickedStop(color);

  public void BuildHandles(OverlayEditorFrame screen, bool reduceMotion) =>
    _gradient.BuildHandles(
      screen,
      () => _formulas.Activate(TextFormulas.AngleKey),
      reduceMotion,
      () => _text.Remember(false),
      () =>
      {
        if (_handleColor == null)
          return;
        _text.SetRangeColor(_handleColor.Clone());
        _host.Render();
      },
      AddStopAt
    );

  public void BuildPicker(UiFactory ui, UiColorPicker picker, bool reduceMotion) =>
    _gradient.BuildPicker(
      ui,
      picker,
      () => _formulas.Activate(TextFormulas.PositionKey),
      reduceMotion,
      () => _preview.BoxSize,
      () =>
      {
        // The dock was typing the previous color's number.
        if (_formulas.Field == TextFormulas.PositionKey || _formulas.Field == TextFormulas.OpacityKey)
          _host.ClosePopovers();
        _host.Render();
      }
    );

  // A click on the gradient line adds a color there.
  private void AddStopAt(float position)
  {
    OverlayColorSettings color = _text.RangeColor()?.Clone();
    if (color == null || !color.IsGradient)
      return;
    _text.Remember(false);
    int added = OverlayGradientEdits.AddStop(color, position, OverlayGradientEditing.NewStopHex);
    if (added < 0)
      return;
    _text.SetRangeColor(color);
    _gradient.Pick(color, added);
    _host.Render();
  }

  /// <summary>The handles over the text's box while the picker shows a gradient, with their numbers.</summary>
  public void Show()
  {
    OverlayColorSettings color = _text.RangeColor();
    if (!_gradient.Refresh(color))
      return;
    if (!_preview.Box(out Rect box))
    {
      Handles.Hide();
      return;
    }
    // The handles keep the color they move until they let go.
    if (!Handles.Dragging || _handleColor == null)
      _handleColor = color.Clone();
    RectTransform frame = _host.Screen.Frame;
    Vector2 topLeft = _preview.InFrame(frame, new Vector3(box.xMin, box.yMax, 0));
    Vector2 bottomRight = _preview.InFrame(frame, new Vector3(box.xMax, box.yMin, 0));
    float scale = (bottomRight.x - topLeft.x) / Mathf.Max(.001f, box.width);
    _gradient.ShowHandles(_handleColor, topLeft, scale, box.width, box.height);
    _formulas.Show(Handles.Angle, TextFormulas.AngleKey, _readAngle);
    _formulas.Show(Position, TextFormulas.PositionKey, _readPosition);
    _gradient.PlaceNumbers();
  }

  // ---- The numbers ----

  /// <summary>How the keypad reads and writes the gradient number open in the dock; null for others.</summary>
  public OverlayFormulaSlot Slot(string field) =>
    field switch
    {
      TextFormulas.PositionKey => _positionSlot,
      TextFormulas.AngleKey => _angleSlot,
      _ => null,
    };

  // A stop's position or opacity in percent: its formula, or the plain number.
  public OverlayFormula ReadStop(bool position) =>
    OverlayGradientEditing.ReadStop(PickedStop(_text.RangeColor()), position);

  public void WriteStop(bool position, OverlayFormula formula)
  {
    _text.RememberFormula(position ? TextFormulas.PositionKey : TextFormulas.OpacityKey);
    OverlayColorSettings color = _text.RangeColor()?.Clone();
    OverlayGradientStop stop = PickedStop(color);
    if (stop == null)
      return;
    OverlayGradientEditing.WriteStop(stop, position, formula);
    OverlayGradientEdits.Sync(color);
    _text.SetRangeColor(color);
  }

  private OverlayFormula ReadAngle()
  {
    Vector2 box = _preview.BoxSize;
    return OverlayGradientEditing.ReadAngle(_text.RangeColor(), box.x, box.y);
  }

  private void WriteAngle(OverlayFormula formula)
  {
    _text.RememberFormula(TextFormulas.AngleKey);
    OverlayColorSettings color = _text.RangeColor()?.Clone();
    if (color == null || !color.IsGradient)
      return;
    Vector2 box = _preview.BoxSize;
    OverlayGradientEditing.WriteAngle(color, formula, box.x, box.y);
    _text.SetRangeColor(color);
  }

  /// <summary>The dock's name for a gradient number; null for others.</summary>
  public string Name(string field) =>
    _gradient.Name(
      field switch
      {
        TextFormulas.PositionKey => OverlayGradientNumber.Position,
        TextFormulas.AngleKey => OverlayGradientNumber.Angle,
        TextFormulas.OpacityKey => OverlayGradientNumber.Opacity,
        _ => OverlayGradientNumber.None,
      },
      _text.RangeColor()
    );
}
