using System;
using Element.Application.Input;
using Element.Application.Layout;
using Element.Configuration;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// Shape editing over a dimmed screen, like the text editor. The shape sits in
/// the middle at its in-game size with W under it and H beside it, and an arc
/// outside each corner for its rounding. Fill, border and where the shape
/// grows from float above. Clicking a number types it in the input dock along
/// the bottom, which is always there.
/// </summary>
/// <remarks>
/// The editor puts its parts together and runs them: <see cref="ShapePreview"/>,
/// <see cref="ShapeMeasures"/>, <see cref="ShapeNumbers"/>, <see cref="ShapeColors"/>,
/// <see cref="ShapeGradient"/>, <see cref="ShapeValueDrops"/> and <see cref="ShapeInput"/>,
/// with the bar, the grow point and undo it shares with the text editor.
/// </remarks>
internal sealed class OverlayShapeEditor : IContentEditor, IShapeEditorHost
{
  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayLiveValues _live;
  private readonly OverlayPreviewSpace _space = new();
  private readonly OverlayEditorHistory<OverlayShapeSettings> _history;
  private readonly ShapePreview _preview;
  private readonly ShapeMeasures _measures = new();
  private readonly ShapeNumbers _numbers;
  private readonly ShapeColors _colors;
  private readonly ShapeGradient _gradient;
  private readonly OverlayGrowFrom _grow;
  private readonly ShapeValueDrops _drops;
  private readonly ShapeInput _input;
  private OverlayEditorFrame _screen;
  private OverlayEditorBar _bar;
  private OverlayInputDock _dock;
  private ShapeOverlayElement _element;
  private Action<bool> _closed;
  private OverlayShapeSettings _shape;
  private OverlayShapeSettings _opening;
  private float _elementScale = 1;
  private float _blinkStart;

  public OverlayShapeEditor(Transform parent, TMP_FontAsset font, Func<bool> reduceMotion, OverlayValues values)
  {
    _parent = parent;
    _font = font;
    _reduceMotion = reduceMotion;
    _live = new OverlayLiveValues(values);
    _history = new OverlayEditorHistory<OverlayShapeSettings>(() => _shape.Clone(), Restore);
    _preview = new ShapePreview(values);
    _numbers = new ShapeNumbers(this, values, _history);
    _colors = new ShapeColors(this, _numbers, _history, font, reduceMotion);
    _gradient = new ShapeGradient(this, values, _colors, _preview, _history, StopSelected);
    _numbers.Use(_gradient);
    _grow = new OverlayGrowFrom(CloseEditing, Render);
    ShapeFieldList fields = new(_measures, _colors, _gradient);
    _drops = new ShapeValueDrops(this, _numbers, _colors, _gradient, _measures, fields);
    _input = new ShapeInput(this, _numbers, _colors, _gradient, _measures, fields, _drops, _grow, _history);
  }

  public bool IsOpen => _screen != null;

  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  OverlayEditorFrame IShapeEditorHost.Screen => _screen;

  OverlayInputDock IShapeEditorHost.Dock => _dock;

  OverlayEditorBar IShapeEditorHost.Bar => _bar;

  OverlayShapeSettings IShapeEditorHost.Shape => _shape;

  float IShapeEditorHost.ElementScale => _elementScale;

  bool IShapeEditorHost.AnyOpen => AnyOpen;

  void IShapeEditorHost.Activate(string key) => Activate(key);

  void IShapeEditorHost.CloseEditing() => CloseEditing();

  void IShapeEditorHost.Render() => Render();

  bool IContentEditor.Open(IOverlayLayoutTarget target, Action<bool> closed)
  {
    if (target is not ShapeOverlayElement shape)
      return false;
    Open(shape, closed);
    return true;
  }

  public void Open(ShapeOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    _element = element;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _shape = (element.Shape ?? OverlayShapeGeometry.Create()).Clone();
    OverlayShapeGeometry.Normalize(_shape);
    _opening = _shape.Clone();
    _grow.Open(element);
    _history.Clear();
    _numbers.Field = null;
    _numbers.Editing.Deactivate();
    Build();
    Render();
  }

  private void Build()
  {
    _colors.DisposePicker();
    _screen?.Dispose();
    _drops.Cancel();
    _space.Snap();
    _screen = new OverlayEditorFrame(
      "Shape editor",
      _parent,
      _font,
      _reduceMotion,
      L(
        "도형 편집 · 숫자를 눌러 입력 · ⌘/Ctrl+Enter나 Esc로 완료",
        "Edit shape · Click a number to type · ⌘/Ctrl+Enter or Esc to finish"
      ),
      () => Close(false, true),
      () => Close(true, true)
    );
    _preview.Build(_screen);
    _measures.Build(_screen, _shape.Kind, Activate, _reduceMotion());
    _gradient.BuildHandles(_screen, _reduceMotion());
    BuildBar();
    _dock = new OverlayInputDock(
      _screen.Ui,
      _screen.Frame,
      _screen.Width,
      _screen.Height,
      _screen.Measure,
      _reduceMotion,
      _numbers.KeypadPress,
      _numbers.ClearFormula,
      _numbers.FinishEditing,
      _numbers.PickValue,
      _drops.BeginValueDrag,
      _drops.MoveValueDrag,
      _drops.EndValueDrag
    );
    _colors.BuildPicker(_screen, _dock);
    _gradient.BuildPicker(_screen.Ui, _reduceMotion());
    _grow.Build(_screen, _reduceMotion());
  }

  // One row of labeled buttons: fill, border, border width and grow point.
  // Colors open their picker (with opacity) right above the keypad.
  private void BuildBar()
  {
    _bar = new OverlayEditorBar(_screen.Ui, _screen.Frame, "Shape bar");
    bool motion = _reduceMotion();
    _bar.Add(_colors.BuildFill(_screen.Ui, _bar.Root, motion));
    _bar.Add(_colors.BuildStroke(_screen.Ui, _bar.Root, motion));
    _bar.Add(_numbers.BuildStrokeWidth(_screen.Ui, _bar.Root, motion));
    _bar.Add(_grow.BuildButton(_screen.Ui, _bar.Root, motion));
  }

  public void Tick()
  {
    if (_screen == null)
      return;
    if (_screen.ViewChanged)
    {
      CloseEditing();
      Build();
      Render();
    }
    _screen.Tick();
    SettingsColorPicker picker = _colors.Picker;
    if (picker.Visible && picker.IsTyping)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
        picker.Hide();
      return;
    }
    _input.HandleMouse();
    _input.HandleKeys();
    if (_screen == null)
      return;
    FollowLiveValues();
    // The shape glides aside as the picker opens, and back as it closes.
    if (_space.Moving)
      RenderKeepingBlink();
    _dock.Blink(_reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f);
  }

  private void RenderKeepingBlink()
  {
    float blinkStart = _blinkStart;
    Render();
    _blinkStart = blinkStart;
  }

  // A level playing behind the editor moves the shape and its numbers.
  private void FollowLiveValues()
  {
    OverlayFormulaEditing editing = _numbers.Editing;
    if (!_live.Changed(_preview.Mask | (editing.Active ? editing.Working.Mask : OverlayValueMask.None)))
      return;
    RenderKeepingBlink();
  }

  private void Render()
  {
    if (_screen == null)
      return;
    _blinkStart = Time.unscaledTime;
    _numbers.Sync();
    _dock.Edit(_numbers.Field);
    // The shape and its bar center in the space left of an open picker.
    _space.Measure(_screen, _colors.Picker, _dock, _reduceMotion(), out float left, out float right);
    _preview.Draw(
      _shape,
      _elementScale,
      _screen,
      _dock,
      OverlayEditorFrame.TopBar + 8 + OverlayEditorBar.Height + 56,
      left,
      right
    );
    LayOutBar(left, right);
    if (_gradient.Show())
      _gradient.ShowNumbers(_numbers);
    _measures.LayOut(
      _preview,
      _numbers,
      OverlayShapeGeometry.HasCorners(_shape.Kind),
      _gradient.Handles.Visible,
      _reduceMotion()
    );
    _colors.ShowOpacity(_gradient.PickedStop);
    PlaceGrowMark();
    string field = _numbers.Field;
    _dock.Refresh(
      _numbers.Editing,
      field == null ? "" : _numbers.Name(field),
      field == null ? null : _numbers.ResultText(field),
      _colors.Hint()
        ?? L(
          "W·H, 테두리, 꼭짓점 호 같은 숫자를 누르면 여기서 입력해요",
          "Click a number (W, H, border or a corner's arc) to type it here"
        )
    );
  }

  // The bar stays over the shape, clear of an open picker.
  private void LayOutBar(float left, float right)
  {
    _colors.ShowButtons(_preview);
    _numbers.ShowStrokeWidth();
    _grow.ShowButton();
    float width = _bar.LayOut();
    _bar.Place(
      Mathf.Max(OverlayEditorFrame.Margin, (left + right - width) * .5f),
      OverlayEditorFrame.TopBar + 8,
      width
    );
    _colors.PlacePicker(_dock);
  }

  // While the grow popover is open, a dot marks the point that stays put.
  private void PlaceGrowMark()
  {
    if (!_grow.Visible)
    {
      _grow.HideMark();
      return;
    }
    OverlayShapeSettings resolved = _preview.Resolved;
    _grow.PlaceMark(
      _preview.TopLeft.x,
      _preview.TopLeft.y,
      resolved.Width * _preview.UnitsToFrame,
      resolved.Height * _preview.UnitsToFrame
    );
  }

  // Clicking a number edits it in the dock; another number switches the dock
  // to it, and the same one again closes it.
  private void Activate(string key)
  {
    if (key == null)
      return;
    bool open = _numbers.Field == key;
    CloseEditing();
    // The picker's own numbers keep it open.
    SettingsColorPicker picker = _colors.Picker;
    if (picker.Visible && !ShapeGradient.PickerKey(key))
      picker.Hide();
    if (!open)
      _numbers.Field = key;
    Render();
  }

  // The dock was typing the previous color's number.
  private void StopSelected()
  {
    string field = _numbers.Field;
    if (
      field != null
      && (ShapeNumbers.Kind(field, out _).EndsWith("position") || ShapeNumbers.Kind(field, out _).EndsWith("opacity"))
    )
      CloseEditing();
    Render();
  }

  private bool AnyOpen => _numbers.Field != null || _grow.Visible;

  private void CloseEditing()
  {
    _numbers.Finish();
    _grow.Hide();
  }

  /// <summary>
  /// Esc closes what is open; with nothing open it finishes like Done. Only
  /// Cancel leaves without saving.
  /// </summary>
  public void Escape()
  {
    SettingsColorPicker picker = _colors.Picker;
    if (picker.Visible)
      picker.Hide();
    else if (AnyOpen)
      CloseEditing();
    else
    {
      Close(true, true);
      return;
    }
    Render();
  }

  // Undo puts a copy back; the dock reads the restored number afresh, and an
  // open picker shows the restored color.
  private void Restore(OverlayShapeSettings shape)
  {
    _shape = shape;
    _history.Break();
    _numbers.Editing.Deactivate();
    _colors.Restored();
    Render();
  }

  /// <summary>Done writes the shape and where it grows from to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && _colors.Picker.Visible && !_colors.Picker.Commit())
      return;
    CloseEditing();
    ShapeOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null)
    {
      if (!_shape.SameAs(_opening))
      {
        element.Settings.Shape = _shape.Clone();
        element.Apply();
      }
      element.SetGrowFrom(_grow.Growth);
    }
    _colors.DisposePicker();
    _screen.Dispose();
    _screen = null;
    _drops.Cancel();
    _numbers.Editing.Deactivate();
    _element = null;
    _closed = null;
    InputGate.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  // Torn down with the editor open (the mod turned off), the edit is kept;
  // a color that can't be read is the only thing that falls back.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }
}
