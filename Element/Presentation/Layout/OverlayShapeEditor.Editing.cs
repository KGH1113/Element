using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Layout;

// Drawing, the numbers the dock edits, keys, picked and dropped values,
// where the shape grows from, and undo.
internal sealed partial class OverlayShapeEditor
{
  private Vector2 _boxTopLeft;
  private float _unitsToFrame = 1;

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
    if (_picker.Visible && _picker.IsTyping)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
        _picker.Hide();
      return;
    }
    HandleMouse();
    HandleKeys();
    if (_screen == null)
      return;
    FollowLiveValues();
    _dock.Blink(_reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f);
  }

  // A level playing behind the editor moves the shape and its numbers.
  private void FollowLiveValues()
  {
    if (!_live.Changed(_resolver.Mask | (_editing.Active ? _editing.Working.Mask : 0)))
      return;
    float blinkStart = _blinkStart;
    Render();
    _blinkStart = blinkStart;
  }

  // Every number on the screen with the key of what it edits.
  private IEnumerable<(OverlayFormulaField Field, string Key)> Fields()
  {
    yield return (_widthField, "width");
    yield return (_heightField, "height");
    for (int i = 0; i < _radiusFields.Count; i++)
      yield return (_radiusFields[i], "radius:" + i);
    if (_picker != null && _picker.Visible)
      yield return (_opacity.Field, OpacityKey);
  }

  // The picker's opacity edits the color it shows.
  private string OpacityKey => _pickerForStroke ? "stroke-opacity" : "fill-opacity";

  // ---- Drawing ----

  private void Render()
  {
    if (_screen == null)
      return;
    _blinkStart = Time.unscaledTime;
    _editing.Activate(_field == null ? null : Slot(_field));
    SyncDock();
    DrawShape();
    LayoutBar();
    LayoutMeasures();
    ShowOpacity();
    PlaceGrowMark();
    _dock.Refresh(
      _editing,
      _field == null ? "" : FieldName(_field),
      _field == null ? null : ResultText(_field),
      _picker.Visible
        ? _pickerForStroke
          ? L(
            "테두리 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
            "Picking the border color · Drop a value on the opacity to calculate it"
          )
          : L(
            "채우기 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
            "Picking the fill color · Drop a value on the opacity to calculate it"
          )
        : L(
          "W·H, 테두리, 꼭짓점 호 같은 숫자를 누르면 여기서 입력해요",
          "Click a number (W, H, border or a corner's arc) to type it here"
        )
    );
  }

  // The shape at its in-game size, made smaller only to fit between the bar
  // and the dock.
  private void DrawShape()
  {
    _resolver.Compile(_shape);
    _resolver.Resolve(_shape, _values.WithSamples, _elementScale, _resolved);
    float width = _screen.Width;
    float top = OverlayEditorFrame.TopBar + 8 + BarHeight + 56,
      bottom = _dock.Top - 28;
    float availableWidth = Mathf.Max(60, width - OverlayEditorFrame.Margin * 2 - 260);
    float availableHeight = Mathf.Max(40, bottom - top - OverlayFormulaField.Height - MeasureGap - 20);
    float units = _elementScale * ScreenScale / _screen.Scale;
    float fit = Mathf.Min(
      1,
      availableWidth / Mathf.Max(1, _resolved.Width * units),
      availableHeight / Mathf.Max(1, _resolved.Height * units)
    );
    _unitsToFrame = units * fit;
    float boxWidth = _resolved.Width * _unitsToFrame,
      boxHeight = _resolved.Height * _unitsToFrame;
    _boxTopLeft = new Vector2((width - boxWidth) * .5f, top + (availableHeight - boxHeight) * .5f);
    float pixels = _unitsToFrame * _screen.Scale;
    _shapeRect.anchorMin = _shapeRect.anchorMax = _shapeRect.pivot = new Vector2(0, 1);
    _shapeRect.anchoredPosition = new Vector2(_boxTopLeft.x * _screen.Scale, -_boxTopLeft.y * _screen.Scale);
    _shapeRect.sizeDelta = new Vector2(_resolved.Width, _resolved.Height);
    _shapeRect.localScale = new Vector3(pixels, pixels, 1);
    IOverlayNumberSource samples = _values.WithSamples;
    _graphic.SetShape(
      _resolved,
      OverlayTextComposer.Resolve(_resolved.Fill, OverlayTheme.Accent, _resolver.FillAlpha, samples),
      OverlayTextComposer.Resolve(_resolved.Stroke, OverlayTheme.Accent, _resolver.StrokeAlpha, samples)
    );
    _graphic.SetPixelsPerUnit(pixels);
  }

  private void LayoutBar()
  {
    float y = (BarHeight - OverlayBarButton.Height) * .5f;
    _fillButton.Swatch = Opaque(_resolved.Fill);
    _fillButton.Set(null, false, _picker.Visible && !_pickerForStroke);
    _strokeButton.Swatch = Opaque(_resolved.Stroke);
    _strokeButton.Set(null, false, _picker.Visible && _pickerForStroke);
    OverlayFormula stroke = _field == "stroke" && _editing.Active ? _editing.Working.Trimmed() : Read("stroke");
    bool computed = !stroke.IsEmpty && !stroke.IsPlainNumber(out _);
    _strokeWidth.Set(stroke.IsEmpty ? "—" : OverlayFormulaField.Number(Result(stroke)), computed, _field == "stroke");
    _growButton.Set(OverlayGrowPicker.ShortName(_growth), false, _growPopover.Visible);
    float x = y;
    foreach (OverlayBarButton button in new[] { _fillButton, _strokeButton, _strokeWidth, _growButton })
    {
      button.Place(x, y);
      x += button.Width + 4;
    }
    x += y - 4;
    SettingsUiFactory.Place(_bar, (_screen.Width - x) * .5f, OverlayEditorFrame.TopBar + 8, x, BarHeight);
    PlacePicker();
  }

  // A number shows its result; one worked out from a formula reads in the accent.
  private void Show(OverlayFormulaField field, string key)
  {
    OverlayFormula formula = _field == key && _editing.Active ? _editing.Working.Trimmed() : Read(key);
    bool computed = !formula.IsPlainNumber(out double plain) && !formula.IsEmpty;
    double shown =
      computed ? Result(formula)
      : formula.IsEmpty ? double.NaN
      : plain;
    field.Set(OverlayFormulaField.Number(shown), computed, _field == key);
  }

  // The picker's opacity shows its result like W and H.
  private void ShowOpacity()
  {
    if (!_picker.Visible)
      return;
    string key = OpacityKey;
    OverlayFormula formula = _field == key && _editing.Active ? _editing.Working.Trimmed() : Read(key);
    OverlayColorSettings color = _pickerForStroke ? _shape.Stroke : _shape.Fill;
    _opacity.Show(formula, Result(formula), color?.Alpha ?? 1, _field == key);
  }

  private double Result(OverlayFormula formula) =>
    formula.TryEvaluate(_values.WithSamples, out double result, out _) ? result : double.NaN;

  private string ResultText(string key)
  {
    OverlayFormula formula = _editing.Active ? _editing.Working.Trimmed() : Read(key);
    if (formula.IsEmpty)
      return null;
    return OverlayFormulaField.Number(Result(formula)) + (Kind(key, out _).EndsWith("opacity") ? "%" : " px");
  }

  private static Color Opaque(OverlayColorSettings color)
  {
    Color resolved = OverlayTextComposer.Resolve(color, OverlayTheme.Accent);
    resolved.a = 1;
    return resolved;
  }

  // W under the shape and H beside it; an arc outside each corner, with the
  // corner's rounding shown while it is edited.
  private void LayoutMeasures()
  {
    float left = _boxTopLeft.x,
      top = _boxTopLeft.y;
    float boxWidth = _resolved.Width * _unitsToFrame,
      boxHeight = _resolved.Height * _unitsToFrame;
    Show(_widthField, "width");
    _widthField.Place(left + boxWidth * .5f - _widthField.Width * .5f, top + boxHeight + MeasureGap);
    Show(_heightField, "height");
    _heightField.Place(left + boxWidth + MeasureGap, top + boxHeight * .5f - OverlayFormulaField.Height * .5f);

    OverlayShapeGeometry.Corners(_resolved, _corners);
    for (int i = 0; i < _arcs.Count; i++)
    {
      bool corner = HasCorners && i < _corners.Count;
      string key = "radius:" + i;
      bool editing = corner && _field == key;
      _arcs[i].gameObject.SetActive(corner);
      _radiusFields[i].SetVisible(editing);
      if (!corner)
        continue;
      Vector2 point = Frame(_corners[i]);
      Vector2 outward = Outward(i);
      RectTransform arc = (RectTransform)_arcs[i].transform;
      SettingsUiFactory.Place(
        arc,
        point.x - OverlayCornerArc.Size * .5f,
        point.y - OverlayCornerArc.Size * .5f,
        OverlayCornerArc.Size,
        OverlayCornerArc.Size
      );
      arc.GetComponent<OverlayCornerArc>().Angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
      OverlayCornerArc.Paint(_arcs[i], editing, _reduceMotion());
      if (!editing)
        continue;
      OverlayFormulaField radius = _radiusFields[i];
      Show(radius, key);
      Vector2 center = point + outward * RadiusOffset;
      radius.Place(center.x - radius.Width * .5f, center.y - OverlayFormulaField.Height * .5f);
      radius.Root.SetAsLastSibling();
    }
  }

  // A box-space point in frame units.
  private Vector2 Frame(NVector2 point) => _boxTopLeft + new Vector2(point.X, point.Y) * _unitsToFrame;

  // Away from the shape across the corner (screen y down).
  private Vector2 Outward(int index)
  {
    int count = _corners.Count;
    NVector2 vertex = _corners[index];
    NVector2 toPrevious = NVector2.Normalize(_corners[(index + count - 1) % count] - vertex);
    NVector2 toNext = NVector2.Normalize(_corners[(index + 1) % count] - vertex);
    NVector2 inward = toPrevious + toNext;
    Vector2 direction = inward.LengthSquared() > 1e-6f ? -new Vector2(inward.X, inward.Y).normalized : Vector2.up;
    // Corners moved in 1.1.0 can point inward; keep the arc outside the middle.
    NVector2 center = NVector2.Zero;
    foreach (NVector2 corner in _corners)
      center += corner;
    center /= count;
    Vector2 away = new(vertex.X - center.X, vertex.Y - center.Y);
    if (Vector2.Dot(direction, away) < 0)
      direction = -direction;
    return direction;
  }

  // While the grow popover is open, a dot marks the point that stays put.
  private void PlaceGrowMark()
  {
    bool show = _growPopover.Visible;
    _growMark.gameObject.SetActive(show);
    if (!show)
      return;
    Anchor9 point = GrowPointNow();
    Vector2 at = new(
      _boxTopLeft.x + _resolved.Width * _unitsToFrame * OverlayLayoutPosition.AnchorX(point),
      _boxTopLeft.y + _resolved.Height * _unitsToFrame * OverlayLayoutPosition.AnchorY(point)
    );
    SettingsUiFactory.Place(_growMark.rectTransform, at.x - 6, at.y - 6, 12, 12);
    _growMark.transform.SetAsLastSibling();
  }

  // ---- The dock and the grow popover ----

  // Clicking a number edits it in the dock; another number switches the dock
  // to it, and the same one again closes it.
  private void Activate(string key)
  {
    if (key == null)
      return;
    bool open = _field == key;
    CloseEditing();
    // The picker's own opacity keeps it open.
    if (_picker.Visible && !key.EndsWith("opacity"))
      _picker.Hide();
    if (!open)
      _field = key;
    Render();
  }

  private void ToggleGrow()
  {
    bool open = _growPopover.Visible;
    CloseEditing();
    if (!open)
    {
      _growPopover.Show(_screen.FrameRect(_growButton.Root), _screen.Width, _screen.Height);
      _growPopover.Refresh(_growth, GrowPointNow());
    }
    Render();
  }

  private void PickGrowth(OverlayGrowth growth)
  {
    _growth = growth;
    _growPopover.Refresh(_growth, GrowPointNow());
    Render();
  }

  // With Auto, the point picked from where the shape sits on screen.
  private Anchor9 GrowPointNow() =>
    OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  private bool AnyOpen => _field != null || _growPopover.Visible;

  private void CloseEditing()
  {
    FinishField();
    _growPopover?.Hide();
  }

  private void SyncDock()
  {
    _dock.Edit(_field);
  }

  private void FinishEditing()
  {
    CloseEditing();
    Render();
  }

  // Starting over is its own undo step.
  private void ClearFormula()
  {
    _editing.Clear();
    _lastKey = null;
    Render();
  }

  private static string FieldName(string key) =>
    Kind(key, out int index) switch
    {
      "width" => L("너비", "Width"),
      "height" => L("높이", "Height"),
      "stroke" => L("테두리 두께", "Border width"),
      "fill-opacity" => L("채우기 불투명도", "Fill opacity"),
      "stroke-opacity" => L("테두리 불투명도", "Border opacity"),
      _ => L("꼭짓점 ", "Corner ") + (index + 1) + L(" 둥글기", " rounding"),
    };

  // A value picked in the dock joins or replaces the open number's formula.
  private void PickValue(OverlayValueDefinition value)
  {
    if (_screen == null)
      return;
    if (!_editing.Active)
    {
      _screen.Notice(L("먼저 값을 넣을 숫자를 고르세요", "Pick the number for it first"), false);
      return;
    }
    if (_editing.Drop(_editing.Slot, value) == OverlayDropResult.RefusedWords)
      RefuseWords(value);
    Render();
  }

  // ---- The numbers the dock edits ----

  private OverlayFormulaSlot Slot(string key) => new(key, false, () => Read(key), formula => Write(key, formula));

  private OverlayFormula Read(string key)
  {
    float scale = _elementScale;
    switch (Kind(key, out int index))
    {
      case "width":
        return Number(_shape.WidthFormula, _shape.Width * scale);
      case "height":
        return Number(_shape.HeightFormula, _shape.Height * scale);
      case "stroke":
        return Number(_shape.StrokeWidthFormula, _shape.StrokeWidth * scale);
      case "fill-opacity":
        return OverlayOpacityField.Read(_shape.Fill);
      case "stroke-opacity":
        return OverlayOpacityField.Read(_shape.Stroke);
      case "radius" when index >= 0 && index < _shape.Points.Count:
        return Number(_shape.Points[index].RadiusFormula, _shape.Points[index].Radius * scale);
      default:
        return new OverlayFormula();
    }
  }

  private static OverlayFormula Number(string formula, float shown) =>
    string.IsNullOrEmpty(formula)
      ? OverlayFormula.FromNumber(Mathf.Round(shown * 10) / 10)
      : OverlayFormula.Parse(formula);

  // A plain number replaces any formula; a formula keeps the number underneath.
  private void Write(string key, OverlayFormula formula)
  {
    Remember(key);
    float scale = _elementScale;
    bool plain = formula.IsPlainNumber(out double value);
    string text = plain ? "" : formula.Serialize();
    float number = (float)value;
    switch (Kind(key, out int index))
    {
      case "width":
        if (plain)
          _shape.Width = Mathf.Clamp(number, 1, MaximumSize) / scale;
        _shape.WidthFormula = text;
        break;
      case "height":
        if (plain)
          _shape.Height = Mathf.Clamp(number, 1, MaximumSize) / scale;
        _shape.HeightFormula = text;
        break;
      case "stroke":
        if (plain)
          _shape.StrokeWidth = Mathf.Clamp(number, 0, MaximumStroke) / scale;
        _shape.StrokeWidthFormula = text;
        break;
      case "fill-opacity":
        _shape.Fill = OverlayOpacityField.Write(_shape.Fill, formula);
        break;
      case "stroke-opacity":
        _shape.Stroke = OverlayOpacityField.Write(_shape.Stroke, formula);
        break;
      case "radius" when index >= 0 && index < _shape.Points.Count:
        if (plain)
        {
          List<NVector2> corners = new();
          OverlayShapeGeometry.Corners(_shape, corners);
          _shape.Points[index].Radius = OverlayShapeGeometry.ClampRadius(corners, index, number / scale);
        }
        _shape.Points[index].RadiusFormula = text;
        break;
    }
  }

  private static string Kind(string key, out int index)
  {
    index = -1;
    int colon = key?.IndexOf(':') ?? -1;
    if (colon < 0)
      return key;
    int.TryParse(key.Substring(colon + 1), out index);
    return key.Substring(0, colon);
  }

  // Leaves a number, saying so when what was typed had to be brought into range.
  private void FinishField()
  {
    if (_field == null)
      return;
    string key = _field;
    OverlayFormula typed = _editing.Working?.Trimmed();
    _field = null;
    _editing.Deactivate();
    if (typed == null || !typed.IsPlainNumber(out double number) || _screen == null)
      return;
    string kind = Kind(key, out _);
    if ((kind == "width" || kind == "height") && (number < 1 || number > MaximumSize))
      _screen.Notice(L("너비와 높이는 1–10000px 사이로 맞췄어요", "Width and height were kept between 1 and 10000px"));
    else if (kind == "stroke" && (number < 0 || number > MaximumStroke))
      _screen.Notice(L("테두리 두께는 0–100px 사이로 맞췄어요", "Border width was kept between 0 and 100px"));
    else if (kind.EndsWith("opacity") && OverlayOpacityField.OutOfRange(typed))
      _screen.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
    else if (kind == "radius" && !Read(key).SameAs(OverlayFormula.FromNumber(number)))
      _screen.Notice(L("둥글기는 이 꼭짓점에 들어가는 만큼만 적용돼요", "Rounding was kept to what fits this corner"));
  }

  private void KeypadPress(char key)
  {
    if (_screen == null)
      return;
    if (key == '\b')
      _editing.Backspace();
    else
      _editing.Press(key);
    Render();
  }

  // ---- Keys and clicks ----

  private void HandleKeys()
  {
    bool mac =
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;
    while (_screen != null && Event.PopEvent(_event))
    {
      if (_event.rawType != EventType.KeyDown)
        continue;
      bool command = mac ? _event.command : _event.control;
      if (command)
      {
        if (_event.keyCode == KeyCode.Return || _event.keyCode == KeyCode.KeypadEnter)
        {
          Close(true, true);
          return;
        }
        if (_event.keyCode == KeyCode.Z)
        {
          if (_event.shift)
            Redo();
          else
            Undo();
        }
        else if (_event.keyCode == KeyCode.Y)
          Redo();
        continue;
      }
      KeyCode code = _event.keyCode;
      if (code == KeyCode.Escape || code == KeyCode.Return || code == KeyCode.KeypadEnter)
      {
        // Enter and Esc close what is open; Esc with nothing open finishes
        // like Done. Only Cancel leaves without saving.
        if (_picker.Visible)
          _picker.Hide();
        else if (AnyOpen)
          CloseEditing();
        else if (code == KeyCode.Escape)
        {
          Close(true, true);
          return;
        }
        Render();
        continue;
      }
      if (_editing.Active && OverlayKeypad.TryKeyboard(_event, out char key))
        KeypadPress(key);
    }
  }

  // A click on the empty dim closes what is open.
  private void HandleMouse()
  {
    if (_dragValue != null || _dock.Dragging || !Input.GetMouseButtonDown(0))
      return;
    Vector2 screen = Input.mousePosition;
    if (OverControl(screen))
      return;
    bool picker = _picker.Visible;
    if (picker)
      _picker.Hide();
    if (!AnyOpen && !picker)
      return;
    CloseEditing();
    Render();
  }

  private bool OverControl(Vector2 screen)
  {
    if (
      OverlayEditorUi.Contains(_bar, screen)
      || _dock.Contains(screen)
      || _growPopover.Contains(screen)
      || OverlayEditorUi.Contains(_picker.Root, screen)
      || _screen.ToFrame(screen).y < OverlayEditorFrame.TopBar
    )
      return true;
    foreach ((OverlayFormulaField field, string _) in Fields())
      if (field.Contains(screen))
        return true;
    foreach (Button arc in _arcs)
      if (OverlayEditorUi.Contains((RectTransform)arc.transform, screen))
        return true;
    return false;
  }

  // ---- Dragging values in ----

  private const string FormulaLine = "formula";

  private void BeginValueDrag(OverlayValueDefinition value, Vector2 screen)
  {
    _dragValue = value;
    MoveValueDrag(screen);
  }

  private void MoveValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    _dropKey = FindDrop(screen, out bool accepted);
    foreach ((OverlayFormulaField field, string key) in Fields())
      field.SetDrop(key == _dropKey ? (accepted ? 1 : -1) : 0);
    _strokeWidth.SetDrop(_dropKey == "stroke" ? (accepted ? 1 : -1) : 0);
    _dock.SetDrop(_dropKey == FormulaLine ? (accepted ? 1 : -1) : 0);
    _screen.ShowGhost(OverlayKeypad.ValueName(_dragValue), screen, accepted || _dropKey == null ? 1 : -1);
  }

  private void EndValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    OverlayValueDefinition value = _dragValue;
    string key = FindDrop(screen, out _);
    _dragValue = null;
    _dropKey = null;
    foreach ((OverlayFormulaField field, string _) in Fields())
      field.SetDrop(0);
    _strokeWidth.SetDrop(0);
    _dock.SetDrop(0);
    _screen.HideGhost();
    if (key == FormulaLine)
    {
      if (_editing.Active && _editing.Drop(_editing.Slot, value) == OverlayDropResult.RefusedWords)
        RefuseWords(value);
    }
    else if (key != null)
    {
      if (!value.IsNumeric)
        RefuseWords(value);
      else
      {
        if (_field != key)
        {
          CloseEditing();
          _field = key;
        }
        _editing.Activate(Slot(key));
        _editing.Drop(_editing.Slot, value);
      }
    }
    Render();
  }

  private void RefuseWords(OverlayValueDefinition value) =>
    _screen.Notice(
      L(
        OverlayKeypad.ValueName(value) + "은 글자라서 도형의 숫자에는 넣을 수 없어요",
        OverlayKeypad.ValueName(value) + " is words, so it can't go in a shape's number"
      )
    );

  private string FindDrop(Vector2 screen, out bool accepted)
  {
    accepted = _dragValue.IsNumeric;
    // The picker sits over everything else; only its opacity takes a value.
    if (_picker.Visible && OverlayEditorUi.Contains(_picker.Root, screen))
    {
      if (_opacity.Contains(screen))
        return OpacityKey;
      accepted = true;
      return null;
    }
    if (_dock.FormulaContains(screen))
    {
      accepted = _editing.Active && OverlayFormulaEditing.Accepts(_editing.Slot, _dragValue);
      return FormulaLine;
    }
    if (_dock.Contains(screen))
    {
      accepted = true;
      return null;
    }
    if (_strokeWidth.Contains(screen))
      return "stroke";
    foreach ((OverlayFormulaField field, string key) in Fields())
      if (field.Root.gameObject.activeInHierarchy && field.Contains(screen))
        return key;
    for (int i = 0; i < _arcs.Count; i++)
      if (_arcs[i].gameObject.activeSelf && OverlayEditorUi.Contains((RectTransform)_arcs[i].transform, screen))
        return "radius:" + i;
    accepted = true;
    return null;
  }

  // ---- Undo ----

  // Edits of one number within a second make one undo step.
  private void Remember(string key)
  {
    bool coalesce = key != null && key == _lastKey && Time.unscaledTime - _lastAt < 1f;
    _lastKey = key;
    _lastAt = Time.unscaledTime;
    if (coalesce && _undo.Count > 0)
      return;
    _undo.Add(_shape.Clone());
    if (_undo.Count > 100)
      _undo.RemoveAt(0);
    _redo.Clear();
  }

  private void Undo()
  {
    if (_undo.Count == 0)
      return;
    _redo.Add(_shape.Clone());
    _shape = _undo[_undo.Count - 1];
    _undo.RemoveAt(_undo.Count - 1);
    Restored();
  }

  private void Redo()
  {
    if (_redo.Count == 0)
      return;
    _undo.Add(_shape.Clone());
    _shape = _redo[_redo.Count - 1];
    _redo.RemoveAt(_redo.Count - 1);
    Restored();
  }

  private void Restored()
  {
    _lastKey = null;
    // The dock reads the restored number afresh.
    string field = _field;
    _editing.Deactivate();
    _field = field;
    _pickerSnapshotTaken = false;
    if (_picker.Visible)
      ShowPicker();
    Render();
  }
}
