using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Values: the input dock and the formula it edits, picking or dragging values
// into the text or the bar's numbers, where the text grows from, and naming
// a value under the pointer.
internal sealed partial class OverlayTextEditor
{
  private const string SizeKey = "size",
    OpacityKey = "opacity";

  private enum DropTarget
  {
    None,
    Size,
    Opacity,
    Formula,
    Join,
    Insert,
  }

  private readonly OverlayFormulaEditing _editing = new();
  private string _fieldSlot;

  // The value whose popover is open, while it stays selected; -1 for none.
  private int _chipPopover = -1;

  private string _pendingSizeFormula = "";
  private string _lastFormulaKey;
  private float _lastFormulaAt = -1;
  private OverlayValueDefinition _dragValue;
  private DropTarget _dropTarget;
  private int _dropIndex;

  // ---- Which formula the keypad edits ----

  // A clicked number field, or the clicked value while it stays selected.
  private OverlayFormulaSlot CurrentSlot()
  {
    if (_fieldSlot == SizeKey)
      return new OverlayFormulaSlot(SizeKey, false, ReadSize, WriteSize);
    if (_fieldSlot == OpacityKey)
      return new OverlayFormulaSlot(OpacityKey, false, ReadOpacity, WriteOpacity);
    return ChipOpen(out int index) ? ChipSlot(index) : null;
  }

  private bool ChipOpen(out int index) => SelectedValue(out index) && index == _chipPopover;

  private OverlayFormulaSlot ChipSlot(int index) =>
    new(
      "value:" + index,
      true,
      () => OverlayFormula.Parse(_document[index].Value),
      formula =>
      {
        RememberFormula("value:" + index);
        _document.SetValue(index, formula.Serialize());
        if (formula.IsEmpty)
          _caretIndex = _anchor = Mathf.Min(index, _document.Count);
      }
    );

  private bool SelectedValue(out int index)
  {
    index = Mathf.Min(_anchor, _caretIndex);
    return Mathf.Abs(_anchor - _caretIndex) == 1 && index < _document.Count && _document[index].IsValue;
  }

  // ---- Opening and closing the popovers ----

  // A second click on the same number closes its popover.
  private void ActivateField(string key)
  {
    bool open = _fieldSlot == key;
    ClosePopovers();
    if (!open)
      _fieldSlot = key;
    Render();
  }

  // A click on a value selects it whole and opens its formula beside it.
  private void OpenChip(int index)
  {
    ClosePopovers();
    _anchor = index;
    _caretIndex = index + 1;
    _chipPopover = index;
    _goalX = float.NaN;
    Render();
  }

  private void ToggleGrow()
  {
    bool open = _growPopover.Visible;
    ClosePopovers();
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

  // With Auto, the point picked from where the text sits on screen.
  private Anchor9 GrowPointNow() =>
    Domain.Layout.OverlayLayoutPosition.ValidGrowth(_growth) ? (Anchor9)(int)_growth : _element.GrowPoint;

  private bool AnyPopover => _fieldSlot != null || _chipPopover >= 0 || _growPopover.Visible;

  private void ClosePopovers()
  {
    FinishField();
    _chipPopover = -1;
    _editing.Deactivate();
    _dock?.Edit(null);
    _growPopover?.Hide();
  }

  // Points the dock at what is being edited, or lets it rest once that is gone.
  private void SyncDock()
  {
    if (_chipPopover >= 0 && !ChipOpen(out _))
    {
      _chipPopover = -1;
      _editing.Deactivate();
    }
    _dock.Edit(
      _fieldSlot != null ? _fieldSlot
      : ChipOpen(out int chip) ? "value:" + chip
      : null
    );
  }

  private void RefreshDock()
  {
    string hint = _picker.Visible
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on the opacity to calculate it"
      )
      : L(
        "값을 누르면 커서 자리에 넣어요 · 글 속 값이나 크기를 누르면 여기서 계산해요",
        "Click a value to add it at the caret · Click a value in the text or a number to calculate here"
      );
    string name =
      _fieldSlot == SizeKey ? L("글자 크기", "Text size")
      : _fieldSlot == OpacityKey ? L("불투명도", "Opacity")
      : L("값", "Value");
    _dock.Refresh(_editing, name, DockResult(), hint);
  }

  // The result of what the dock types, as the text or the field will show it.
  private string DockResult()
  {
    if (!_editing.Active)
      return null;
    OverlayFormula formula = _editing.Working.Trimmed();
    if (formula.IsEmpty)
      return null;
    if (_fieldSlot == null)
    {
      _builder.Clear();
      OverlayFormulaText.Append(_builder, formula, _values, true);
      return _builder.Length > 0 ? _builder.ToString() : null;
    }
    return OverlayFormulaField.Number(Result(formula)) + (_fieldSlot == OpacityKey ? "%" : " px");
  }

  private double Result(OverlayFormula formula) =>
    formula.IsPlainNumber(out double plain) ? plain
    : formula.TryEvaluate(_values.WithSamples, out double result, out _) ? result
    : double.NaN;

  private void FinishEditing()
  {
    ClosePopovers();
    Render();
  }

  // Starting over is its own undo step.
  private void ClearFormula()
  {
    _editing.Clear();
    _lastFormulaKey = null;
    Render();
  }

  // A value picked in the dock joins or replaces the formula being typed;
  // with none, it is added at the caret and typing goes on after it.
  private void PickValue(OverlayValueDefinition value)
  {
    if (_screen == null)
      return;
    if (_editing.Active)
    {
      if (_editing.Drop(_editing.Slot, value) == OverlayDropResult.RefusedWords)
        RefuseWords(value);
    }
    else
    {
      InsertValue(value, Mathf.Min(_anchor, _caretIndex), Mathf.Max(_anchor, _caretIndex));
      _anchor = _caretIndex;
    }
    Render();
  }

  // Leaves a number field, saying so when what was typed had to be brought into range.
  private void FinishField()
  {
    if (_fieldSlot == null)
      return;
    string key = _fieldSlot;
    OverlayFormula typed = _editing.Working?.Trimmed();
    _fieldSlot = null;
    _editing.Deactivate();
    if (typed != null && typed.IsPlainNumber(out double number))
    {
      if (key == SizeKey && (number < MinimumSize || number > MaximumSize))
        _screen?.Notice(L("글자 크기는 4–400px 사이로 맞췄어요", "Text size was kept between 4 and 400px"));
      else if (key == OpacityKey && OverlayOpacityField.OutOfRange(typed))
        _screen?.Notice(L("불투명도는 0–100% 사이로 맞췄어요", "Opacity was kept between 0 and 100%"));
    }
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

  // Keypad edits of one place within a second make one undo step.
  private void RememberFormula(string key)
  {
    bool coalesce = key == _lastFormulaKey && Time.unscaledTime - _lastFormulaAt < 1f;
    Remember(coalesce);
    _lastFormulaKey = key;
    _lastFormulaAt = Time.unscaledTime;
  }

  private OverlayFormula ReadSize()
  {
    Range(out int start, out int end);
    OverlayTextItem? first = start < end ? _document[start] : null;
    string formula =
      first?.SizeFormula ?? (_document.Count == 0 ? _pendingSizeFormula : _document.TypingSizeFormula(_caretIndex));
    if (!string.IsNullOrEmpty(formula))
      return OverlayFormula.Parse(formula);
    return SameSize(start, end)
      ? OverlayFormula.FromNumber(Mathf.Round(ShownSize(start, end) * 10) / 10)
      : new OverlayFormula();
  }

  private void WriteSize(OverlayFormula formula)
  {
    RememberFormula(SizeKey);
    Range(out int start, out int end);
    float size;
    string text = "";
    if (formula.IsPlainNumber(out double pixels))
      size = Mathf.Clamp((float)pixels, MinimumSize, MaximumSize) / _elementScale;
    else
    {
      // The plain size stays underneath for when the values have no data.
      size = start < end ? _document[start].Size : TypingSize(_caretIndex);
      text = formula.Serialize();
    }
    if (start < end)
      _document.SetSize(start, end, size, text);
    else
    {
      _pendingSize = size;
      _pendingSizeFormula = text;
    }
  }

  private OverlayFormula ReadOpacity() => OverlayOpacityField.Read(RangeColor());

  private void WriteOpacity(OverlayFormula formula)
  {
    RememberFormula(OpacityKey);
    Range(out int start, out int end);
    OverlayColorSettings color = OverlayOpacityField.Write(RangeColor(), formula);
    if (start < end)
      _document.SetAlpha(start, end, color.Alpha, color.AlphaFormula);
    else
      _pendingColor = color;
  }

  // ---- Shown values of the bar's fields ----

  private void ShowFields()
  {
    // The size shows its result; one worked out from a formula reads in the accent.
    bool active = _fieldSlot == SizeKey;
    OverlayFormula formula = active && _editing.Active ? _editing.Working.Trimmed() : ReadSize();
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    _sizeButton.Set(formula.IsEmpty ? "—" : OverlayFormulaField.Number(Result(formula)), computed, active);
    _colorButton.Set(null, false, _picker.Visible);
    _growButton.Set(OverlayGrowPicker.ShortName(_growth), false, _growPopover.Visible);
    if (!_picker.Visible)
      return;
    // The picker's opacity shows its result the same way.
    bool opacity = _fieldSlot == OpacityKey;
    OverlayFormula alpha = opacity && _editing.Active ? _editing.Working.Trimmed() : ReadOpacity();
    _opacity.Show(alpha, Result(alpha), RangeColor().Alpha, opacity);
  }

  private bool SameSize(int start, int end)
  {
    float size = start < end ? _document[start].Size : TypingSize(_caretIndex);
    for (int i = start + 1; i < end; i++)
      if (_document[i].Size != size || _document[i].SizeFormula.Length > 0)
        return false;
    return true;
  }

  // The size in pixels as the game draws it.
  private float ShownSize(int start, int end)
  {
    float size = start < end ? _document[start].Size : TypingSize(_caretIndex);
    return (size > 0 ? size : OverlayElementDefaults.TextSize) * _elementScale;
  }

  // ---- Dragging values in ----

  private void BeginValueDrag(OverlayValueDefinition value, Vector2 screen)
  {
    _dragValue = value;
    _screen.HideTip();
    MoveValueDrag(screen);
  }

  private void MoveValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    _dropTarget = FindDrop(screen, out _dropIndex, out bool accepted);
    _sizeButton.SetDrop(_dropTarget == DropTarget.Size ? (accepted ? 1 : -1) : 0);
    _opacity.Field.SetDrop(_dropTarget == DropTarget.Opacity ? (accepted ? 1 : -1) : 0);
    _dock.SetDrop(_dropTarget == DropTarget.Formula ? (accepted ? 1 : -1) : 0);
    _screen.ShowGhost(OverlayKeypad.ValueName(_dragValue), screen, accepted || _dropTarget == DropTarget.None ? 1 : -1);
    PlaceDropCaret();
    PlaceMarks();
  }

  private void EndValueDrag(Vector2 screen)
  {
    if (_dragValue == null || _screen == null)
      return;
    OverlayValueDefinition value = _dragValue;
    DropTarget target = FindDrop(screen, out int index, out _);
    _dragValue = null;
    _dropTarget = DropTarget.None;
    _sizeButton.SetDrop(0);
    _opacity.Field.SetDrop(0);
    _dock.SetDrop(0);
    _screen.HideGhost();
    switch (target)
    {
      case DropTarget.Size:
        DropOn(SizeKey, value);
        break;
      case DropTarget.Opacity:
        DropOn(OpacityKey, value);
        break;
      case DropTarget.Formula:
        if (_editing.Active && _editing.Drop(_editing.Slot, value) == OverlayDropResult.RefusedWords)
          RefuseWords(value);
        break;
      case DropTarget.Join:
        _editing.Drop(ChipSlot(index), value);
        break;
      case DropTarget.Insert:
        // A value dropped into the text lands there; typing goes on after it.
        ClosePopovers();
        InsertValue(value, index, index);
        _anchor = _caretIndex;
        break;
    }
    Render();
  }

  private void DropOn(string key, OverlayValueDefinition value)
  {
    if (!value.IsNumeric)
    {
      RefuseWords(value);
      return;
    }
    if (_fieldSlot != key)
    {
      ClosePopovers();
      _fieldSlot = key;
    }
    _editing.Drop(CurrentSlot(), value);
  }

  private void RefuseWords(OverlayValueDefinition value) =>
    _screen.Notice(
      L(
        OverlayKeypad.ValueName(value) + "은 글자라서 계산에 쓸 수 없어요. 글 안에 넣어 주세요.",
        OverlayKeypad.ValueName(value) + " is words, so it can't be calculated. Put it in the text instead."
      )
    );

  // A new value replaces the range and stays selected.
  private void InsertValue(OverlayValueDefinition value, int start, int end)
  {
    Remember(false);
    start = _document.Clamp(start);
    int after = _document.InsertValue(
      start,
      end,
      value.Key,
      InsertColor(value.Id),
      TypingSize(start),
      TypingSizeFormula(start)
    );
    _anchor = start;
    _caretIndex = after;
    _goalX = float.NaN;
  }

  private DropTarget FindDrop(Vector2 screen, out int index, out bool accepted)
  {
    index = -1;
    accepted = true;
    // The picker sits over everything else; only its opacity takes a value.
    if (_picker.Visible && OverlayEditorUi.Contains(_picker.Root, screen))
    {
      bool opacity = _opacity.Contains(screen);
      accepted = !opacity || _dragValue.IsNumeric;
      return opacity ? DropTarget.Opacity : DropTarget.None;
    }
    if (_sizeButton.Contains(screen))
    {
      accepted = _dragValue.IsNumeric;
      return DropTarget.Size;
    }
    if (_dock.FormulaContains(screen))
    {
      accepted = _editing.Active && OverlayFormulaEditing.Accepts(_editing.Slot, _dragValue);
      return DropTarget.Formula;
    }
    if (_dock.Contains(screen) || !InTextArea(screen))
      return DropTarget.None;
    // Onto the value whose formula waits for its next number or value.
    if (
      _editing.Active
      && _editing.Working.AwaitsOperand
      && ChipOpen(out int selected)
      && ItemContains(selected, screen)
    )
    {
      index = selected;
      accepted = _dragValue.IsNumeric;
      return DropTarget.Join;
    }
    index = IndexAt(screen);
    return DropTarget.Insert;
  }

  // An accent caret where a dragged value would be inserted.
  private void PlaceDropCaret()
  {
    bool show = _dragValue != null && _dropTarget == DropTarget.Insert;
    _dropCaret.enabled = show;
    if (!show)
      return;
    CaretGeometry(_dropIndex, out float x, out int line);
    LineExtent(line, out float top, out float bottom);
    float width = 3 / Mathf.Max(.01f, _previewScale);
    RectTransform caret = _dropCaret.rectTransform;
    caret.anchoredPosition = new Vector2(x - width * .5f, bottom);
    caret.sizeDelta = new Vector2(width, top - bottom);
    _dropCaret.color = SettingsPalette.SoftAccent;
    _dropCaret.transform.SetAsLastSibling();
  }

  // ---- Naming the value under the pointer ----

  private void UpdateHover(Vector2 screen)
  {
    if (
      _dragValue != null
      || _dragging
      || _dock.Contains(screen)
      || _growPopover.Contains(screen)
      || !TryValueAt(screen, out int index)
    )
    {
      _screen.HideTip();
      return;
    }
    OverlayFormula formula = OverlayFormula.Parse(_document[index].Value);
    string words = formula.Describe(OverlayKeypad.ValueName);
    if (_document[index].SizeFormula.Length > 0)
      words +=
        L(" · 크기 ", " · size ")
        + OverlayFormula.Parse(_document[index].SizeFormula).Describe(OverlayKeypad.ValueName);
    if (!ItemTop(index, out Vector2 anchor))
    {
      _screen.HideTip();
      return;
    }
    _screen.ShowTip(words, anchor);
  }

  private bool TryValueAt(Vector2 screen, out int index)
  {
    for (index = 0; index < _document.Count; index++)
      if (_document[index].IsValue && ItemContains(index, screen))
        return true;
    index = -1;
    return false;
  }

  private bool ItemContains(int item, Vector2 screen)
  {
    if (!ItemRect(item, out Rect local))
      return false;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(_preview.rectTransform, screen, null, out Vector2 point);
    float padding = 3 / Mathf.Max(.01f, _previewScale);
    local.xMin -= padding;
    local.xMax += padding;
    return local.Contains(point);
  }

  // The item's box in frame units, for placing its popover.
  private bool ItemFrame(int item, out Rect frame)
  {
    frame = default;
    if (!ItemRect(item, out Rect local))
      return false;
    RectTransform rect = _preview.rectTransform;
    Vector2 a = _screen.ToFrame(
      RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(local.xMin, local.yMax, 0)))
    );
    Vector2 b = _screen.ToFrame(
      RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(local.xMax, local.yMin, 0)))
    );
    frame = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    return true;
  }

  // The item's top center in frame units.
  private bool ItemTop(int item, out Vector2 anchor)
  {
    anchor = default;
    if (!ItemRect(item, out Rect local))
      return false;
    Vector3 world = _preview.rectTransform.TransformPoint(new Vector3(local.center.x, local.yMax, 0));
    anchor = _screen.ToFrame(RectTransformUtility.WorldToScreenPoint(null, world));
    return true;
  }

  // The item's box in the text's local space (y up).
  private bool ItemRect(int item, out Rect rect)
  {
    rect = default;
    if (item < 0 || item >= _length.Count || _length[item] == 0 || !TryCharacter(_start[item], out var first))
      return false;
    TryCharacter(_start[item] + _length[item] - 1, out var last);
    LineExtent(first.lineNumber, out float top, out float bottom);
    rect = Rect.MinMaxRect(first.origin, bottom, Mathf.Max(last.xAdvance, first.origin + 2), top);
    return true;
  }
}
