using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using ElementKit.Elements;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Drawing the element and its spots, the numbers the dock edits, choices and
// toggles, keys, dropped values, where the element grows from, and undo.
internal sealed partial class OverlayPropertyEditor
{
  private const string NumberPrefix = "number:";
  private const string FormulaLine = "formula";
  private Vector2 _boxTopLeft;
  private Vector2 _size = new(160, 64);
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
    // A level playing behind, or the mod's own data, moves what the element shows.
    bool live = _live.Changed(_props.Mask | (_editing.Active ? _editing.Working.Mask : OverlayValueMask.None));
    if (live || _type.Revision != _typeRevision || _type.DrawsEveryFrame || _space.Moving)
      RenderKeepingBlink();
    _dock.Blink(_reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f);
  }

  private void RenderKeepingBlink()
  {
    float blinkStart = _blinkStart;
    Render();
    _blinkStart = blinkStart;
  }

  // Every number on the screen with the key of what it edits.
  private IEnumerable<(OverlayFormulaField Field, string Key)> Fields()
  {
    EditorLayout layout = Layout;
    if (_belowField != null)
      yield return (_belowField, NumberKey(layout.BelowItem));
    if (_besideField != null)
      yield return (_besideField, NumberKey(layout.BesideItem));
    if (_picker != null && _picker.Visible)
      yield return (_opacity.Field, OpacityKey);
    if (_picker != null && _picker.IsGradient)
      yield return (_position, PositionKey);
  }

  private static string NumberKey(NumberProperty property) => NumberPrefix + property.Key;

  private NumberProperty NumberOf(string key)
  {
    if (key == null || !key.StartsWith(NumberPrefix, StringComparison.Ordinal))
      return null;
    string name = key.Substring(NumberPrefix.Length);
    foreach (Property property in _type.Properties)
      if (property is NumberProperty number && number.Key == name)
        return number;
    return null;
  }

  // ---- Drawing ----

  private void Render()
  {
    if (_screen == null)
      return;
    _blinkStart = Time.unscaledTime;
    _editing.Activate(_field == null ? null : Slot(_field));
    _dock.Edit(_field);
    DrawElement();
    LayoutBar();
    ShowGradient();
    LayoutMeasures();
    ShowOpacity();
    PlaceGrowMark();
    _dock.Refresh(
      _editing,
      _field == null ? "" : FieldName(_field),
      _field == null ? null : ResultText(_field),
      _picker.Visible
        ? L(
          Name(_pickedColor) + "을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
          "Picking " + Name(_pickedColor) + " · Drop a value on the opacity to calculate it"
        )
        : L("숫자를 누르면 여기서 입력해요", "Click a number to type it here")
    );
  }

  // The element as the mod draws it, at its in-game size, made smaller only
  // to fit between the bar and the dock. Values show their samples.
  private void DrawElement()
  {
    _typeRevision = _type.Revision;
    _props.Compile(_type, _working);
    _props.Use(_values.WithSamples, true, OverlayTheme.Accent);
    Vector2 measured = _size;
    bool failed = false;
    try
    {
      measured = _type.RunMeasure(_props);
    }
    catch (Exception)
    {
      failed = true;
    }
    if (!failed)
      _size = new Vector2(
        float.IsNaN(measured.x) ? 1 : Mathf.Clamp(measured.x, 1, 4000),
        float.IsNaN(measured.y) ? 1 : Mathf.Clamp(measured.y, 1, 4000)
      );
    float top = OverlayEditorFrame.TopBar + 8 + BarHeight + 56,
      bottom = _dock.Top - 28;
    PreviewSpace(out float spaceLeft, out float spaceRight);
    float availableWidth = Mathf.Max(60, spaceRight - spaceLeft - (_besideField != null ? 260 : 80));
    float availableHeight = Mathf.Max(
      40,
      bottom - top - (_belowField != null ? OverlayFormulaField.Height + MeasureGap : 0) - 20
    );
    float units = _elementScale * ScreenScale / _screen.Scale;
    float fit = Mathf.Min(
      1,
      availableWidth / Mathf.Max(1, _size.x * units),
      availableHeight / Mathf.Max(1, _size.y * units)
    );
    _unitsToFrame = units * fit;
    float boxWidth = _size.x * _unitsToFrame,
      boxHeight = _size.y * _unitsToFrame;
    _boxTopLeft = new Vector2((spaceLeft + spaceRight - boxWidth) * .5f, top + (availableHeight - boxHeight) * .5f);
    float pixels = _unitsToFrame * _screen.Scale;
    _preview.anchorMin = _preview.anchorMax = _preview.pivot = new Vector2(0, 1);
    _preview.anchoredPosition = new Vector2(_boxTopLeft.x * _screen.Scale, -_boxTopLeft.y * _screen.Scale);
    _preview.sizeDelta = _size;
    _preview.localScale = new Vector3(pixels, pixels, 1);
    if (failed)
    {
      _surface.Clear();
      DrawFailed();
      return;
    }
    _surface.Restore();
    _surface.Begin(_size, pixels);
    try
    {
      _type.RunDraw(_surface, _props);
      _surface.End();
      _drawFailed = false;
    }
    catch (Exception)
    {
      _surface.Clear();
      DrawFailed();
    }
  }

  // Said once per opening; the element's own refresh logs the details.
  private void DrawFailed()
  {
    if (_drawFailed)
      return;
    _drawFailed = true;
    string mod = _type.ModId;
    _screen.Notice(
      L(
        mod + " 모드가 이 요소를 그리다 오류가 났어요. 모드를 업데이트하거나 만든 분께 알려 주세요.",
        mod + " stopped while drawing this element. Update the mod or tell its author."
      )
    );
  }

  private void LayoutBar()
  {
    float y = (BarHeight - OverlayBarButton.Height) * .5f;
    float x = y;
    foreach ((Property property, OverlayBarButton button) in _barButtons)
    {
      switch (property)
      {
        case ColorProperty color:
          OverlayColorSettings settings = ColorOf(color);
          Color swatch = OverlayTextComposer.Resolve(settings, OverlayTheme.Accent);
          swatch.a = 1;
          button.Swatch = swatch;
          button.Set(null, false, _picker.Visible && _pickedColor == color);
          break;
        case NumberProperty number:
          string key = NumberKey(number);
          OverlayFormula formula = _field == key && _editing.Active ? _editing.Working.Trimmed() : Read(key);
          bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
          button.Set(formula.IsEmpty ? "—" : OverlayFormulaField.Number(Result(formula)), computed, _field == key);
          break;
        case ChoiceProperty choice:
          button.Set(
            Name(choice.Options[ModPropertyValues.Choice(choice, _working.Find(choice.Key))]),
            false,
            _choicePopover.Owner == choice
          );
          break;
        case ToggleProperty toggle:
          button.Set(
            ModPropertyValues.Toggle(toggle, _working.Find(toggle.Key)) ? L("켬", "On") : L("끔", "Off"),
            false,
            false
          );
          break;
      }
      button.Place(x, y);
      x += button.Width + 4;
    }
    _growButton.Set(OverlayGrowPicker.ShortName(_growth), false, _growPopover.Visible);
    _growButton.Place(x, y);
    x += _growButton.Width + y;
    PreviewSpace(out float spaceLeft, out float spaceRight);
    float barLeft = Mathf.Max(OverlayEditorFrame.Margin, (spaceLeft + spaceRight - x) * .5f);
    SettingsUiFactory.Place(_bar, barLeft, OverlayEditorFrame.TopBar + 8, x, BarHeight);
    if (_dock != null)
      OverlayEditorUi.PlaceAboveKeypad(_picker, _dock);
  }

  private static string Name(ChoiceOption option) => L(option.KoreanName, option.EnglishName);

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

  // The mod's numbers under and beside the element, like a shape's W and H.
  private void LayoutMeasures()
  {
    float boxWidth = _size.x * _unitsToFrame,
      boxHeight = _size.y * _unitsToFrame;
    EditorLayout layout = Layout;
    if (_belowField != null)
    {
      Show(_belowField, NumberKey(layout.BelowItem));
      _belowField.Place(
        _boxTopLeft.x + boxWidth * .5f - _belowField.Width * .5f,
        _boxTopLeft.y + boxHeight + MeasureGap
      );
    }
    if (_besideField != null)
    {
      Show(_besideField, NumberKey(layout.BesideItem));
      _besideField.Place(
        _boxTopLeft.x + boxWidth + MeasureGap,
        _boxTopLeft.y + boxHeight * .5f - OverlayFormulaField.Height * .5f
      );
    }
  }

  // The width the element and its bar center in: the screen, or the space
  // left of an open picker.
  private void PreviewSpace(out float left, out float right)
  {
    left = OverlayEditorFrame.Margin;
    right = _screen.Width - OverlayEditorFrame.Margin;
    if (_picker != null && _picker.Visible)
      right = Mathf.Min(right, OverlayEditorUi.PickerLeft(_picker, _dock) - 16);
    right = _space.Toward(right, _reduceMotion());
  }

  private void PlaceGrowMark()
  {
    bool show = _growPopover.Visible;
    _growMark.gameObject.SetActive(show);
    if (!show)
      return;
    Anchor9 point = GrowPointNow();
    Vector2 at = new(
      _boxTopLeft.x + _size.x * _unitsToFrame * OverlayLayoutPosition.AnchorX(point),
      _boxTopLeft.y + _size.y * _unitsToFrame * OverlayLayoutPosition.AnchorY(point)
    );
    SettingsUiFactory.Place(_growMark.rectTransform, at.x - 6, at.y - 6, 12, 12);
    _growMark.transform.SetAsLastSibling();
  }

  private double Result(OverlayFormula formula) =>
    formula.TryEvaluate(_values.WithSamples, out double result, out _) ? result : double.NaN;

  private string ResultText(string key)
  {
    OverlayFormula formula = _editing.Active ? _editing.Working.Trimmed() : Read(key);
    if (formula.IsEmpty)
      return null;
    NumberProperty number = NumberOf(key);
    string unit =
      number != null ? Unit(number)
      : key == OpacityKey || key == PositionKey ? "%"
      : "";
    return OverlayFormulaField.Number(Result(formula))
      + (
        unit.Length == 0 ? ""
        : unit == "px" ? " px"
        : unit
      );
  }

  private string FieldName(string key)
  {
    NumberProperty number = NumberOf(key);
    if (number != null)
      return Name(number);
    if (key == PositionKey)
      return L(PickedRank + "번째 색 위치", "Color " + PickedRank + " position");
    if (key == OpacityKey && _pickedColor != null)
      return PickedStop != null
        ? L(PickedRank + "번째 색 불투명도", "Color " + PickedRank + " opacity")
        : L(Name(_pickedColor) + " 불투명도", Name(_pickedColor) + " opacity");
    return "";
  }

  // ---- The dock, choices and toggles ----

  // Clicking a number edits it in the dock; another number switches the dock
  // to it, and the same one again closes it.
  private void Activate(string key)
  {
    if (key == null)
      return;
    bool open = _field == key;
    CloseEditing();
    if (_picker.Visible && !PickerKey(key))
      _picker.Hide();
    if (!open)
      _field = key;
    Render();
  }

  private void ToggleChoice(ChoiceProperty choice)
  {
    bool open = _choicePopover.Owner == choice;
    CloseEditing();
    if (_picker.Visible)
      _picker.Hide();
    if (!open)
    {
      OverlayBarButton button = ButtonOf(choice);
      List<string> options = new(choice.Options.Count);
      foreach (ChoiceOption option in choice.Options)
        options.Add(Name(option));
      _choicePopover.Show(
        choice,
        Name(choice),
        options,
        ModPropertyValues.Choice(choice, _working.Find(choice.Key)),
        _screen.FrameRect(button.Root),
        _screen.Width,
        _screen.Height
      );
    }
    Render();
  }

  private void PickChoice(int index)
  {
    if (_choicePopover.Owner is not ChoiceProperty choice)
      return;
    if (ModPropertyValues.Choice(choice, _working.Find(choice.Key)) != index)
    {
      Remember(null);
      ModPropertyValues.WriteChoice(_working, choice, index);
    }
    _choicePopover.Hide();
    Render();
  }

  // A toggle flips on click; the button says which way it is.
  private void Flip(ToggleProperty toggle)
  {
    CloseEditing();
    Remember(null);
    ModPropertyValues.WriteToggle(_working, toggle, !ModPropertyValues.Toggle(toggle, _working.Find(toggle.Key)));
    Render();
  }

  private OverlayBarButton ButtonOf(Property property)
  {
    foreach ((Property item, OverlayBarButton button) in _barButtons)
      if (item == property)
        return button;
    return _growButton;
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

  private Anchor9 GrowPointNow() =>
    OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  private bool AnyOpen => _field != null || _growPopover.Visible || _choicePopover.Visible;

  private void CloseEditing()
  {
    FinishField();
    _growPopover?.Hide();
    _choicePopover?.Hide();
  }

  private void FinishEditing()
  {
    CloseEditing();
    Render();
  }

  private void ClearFormula()
  {
    _editing.Clear();
    _lastKey = null;
    Render();
  }

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
    OverlayFormula color = ReadColorNumber(key);
    if (color != null)
      return color;
    NumberProperty number = NumberOf(key);
    return number == null ? new OverlayFormula() : ModPropertyValues.NumberFormula(number, _working.Find(number.Key));
  }

  private void Write(string key, OverlayFormula formula)
  {
    Remember(key);
    if (WriteColorNumber(key, formula))
      return;
    NumberProperty number = NumberOf(key);
    if (number != null)
      ModPropertyValues.WriteNumber(_working, number, formula);
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
    if (typed == null || !typed.IsPlainNumber(out double value) || _screen == null)
      return;
    NumberProperty number = NumberOf(key);
    if (number != null && (value < number.Min || value > number.Max))
      _screen.Notice(
        L(
          Name(number)
            + "은 "
            + OverlayFormulaField.Number(number.Min)
            + "–"
            + OverlayFormulaField.Number(number.Max)
            + Unit(number)
            + " 사이로 맞췄어요",
          Name(number)
            + " was kept between "
            + OverlayFormulaField.Number(number.Min)
            + " and "
            + OverlayFormulaField.Number(number.Max)
            + Unit(number)
        )
      );
    else if (key == OpacityKey && OverlayOpacityField.OutOfRange(typed))
      _screen.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
    else if (key == PositionKey && (value < 0 || value > 100))
      _screen.Notice(L("색 위치는 0–100% 사이로 맞췄어요", "The color's position was kept between 0 and 100%"));
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
      if (GradientKey(code))
        continue;
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
      || _choicePopover.Contains(screen)
      || OverlayEditorUi.Contains(_picker.Root, screen)
      || _screen.ToFrame(screen).y < OverlayEditorFrame.TopBar
    )
      return true;
    foreach ((OverlayFormulaField field, string _) in Fields())
      if (field.Contains(screen))
        return true;
    return false;
  }

  // ---- Dragging values in ----

  private void BeginValueDrag(OverlayValueDefinition value, Vector2 screen)
  {
    _dragValue = value;
    MoveValueDrag(screen);
  }

  private void MoveValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    string drop = FindDrop(screen, out bool accepted);
    foreach ((OverlayFormulaField field, string key) in Fields())
      field.SetDrop(key == drop ? (accepted ? 1 : -1) : 0);
    foreach ((Property property, OverlayBarButton button) in _barButtons)
      button.SetDrop(property is NumberProperty number && NumberKey(number) == drop ? (accepted ? 1 : -1) : 0);
    _dock.SetDrop(drop == FormulaLine ? (accepted ? 1 : -1) : 0);
    _screen.ShowGhost(OverlayKeypad.ValueName(_dragValue), screen, accepted || drop == null ? 1 : -1);
  }

  private void EndValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    OverlayValueDefinition value = _dragValue;
    string key = FindDrop(screen, out _);
    _dragValue = null;
    foreach ((OverlayFormulaField field, string _) in Fields())
      field.SetDrop(0);
    foreach ((Property _, OverlayBarButton button) in _barButtons)
      button.SetDrop(0);
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
        OverlayKeypad.ValueName(value) + "은 글자라서 숫자에는 넣을 수 없어요",
        OverlayKeypad.ValueName(value) + " is words, so it can't go in a number"
      )
    );

  private string FindDrop(Vector2 screen, out bool accepted)
  {
    accepted = _dragValue.IsNumeric;
    if (_picker.Visible && OverlayEditorUi.Contains(_picker.Root, screen))
    {
      if (_opacity.Contains(screen))
        return OpacityKey;
      if (_picker.IsGradient && _position.Contains(screen))
        return PositionKey;
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
    foreach ((Property property, OverlayBarButton button) in _barButtons)
      if (property is NumberProperty number && button.Contains(screen))
        return NumberKey(number);
    foreach ((OverlayFormulaField field, string key) in Fields())
      if (field.Root.gameObject.activeInHierarchy && field.Contains(screen))
        return key;
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
    _undo.Add(_working.Clone());
    if (_undo.Count > 100)
      _undo.RemoveAt(0);
    _redo.Clear();
  }

  private void Undo()
  {
    if (_undo.Count == 0)
      return;
    _redo.Add(_working.Clone());
    _working = _undo[_undo.Count - 1];
    _undo.RemoveAt(_undo.Count - 1);
    Restored();
  }

  private void Redo()
  {
    if (_redo.Count == 0)
      return;
    _undo.Add(_working.Clone());
    _working = _redo[_redo.Count - 1];
    _redo.RemoveAt(_redo.Count - 1);
    Restored();
  }

  private void Restored()
  {
    _lastKey = null;
    string field = _field;
    _editing.Deactivate();
    _field = field;
    _pickerSnapshotTaken = false;
    if (_picker.Visible && _pickedColor != null)
      ShowPicker(keepStop: true);
    Render();
  }
}
