using System;
using System.Collections.Generic;
using System.Text;
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

namespace Element.Presentation.Layout;

/// <summary>
/// Text editing over a dimmed screen. Nothing sits on a card: the text floats
/// in the middle at the size it has in the game, a size, color and opacity
/// bar floats above it. The input dock along the bottom is always there:
/// clicking one of its values adds it at the caret, and clicking a value in
/// the text or a number in the bar types its formula there.
/// </summary>
internal sealed partial class OverlayTextEditor : IDisposable
{
  private const float FormatHeight = 40,
    MinimumSize = 4,
    MaximumSize = 400;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _uiFont;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(256);
  private readonly List<Image> _selectionRects = new();
  private readonly List<Image> _pillRects = new();
  private OverlayEditorFrame _screen;
  private SettingsUiFactory _ui;
  private RectTransform _frame;
  private TextMeshProUGUI _preview;
  private TextMeshProUGUI _placeholder;
  private Image _caret;
  private Image _dropCaret;
  private RectTransform _formatBar;
  private SettingsCapsuleGraphic _formatSurface;
  private OverlayBarButton _sizeButton;
  private OverlayBarButton _colorButton;
  private OverlayBarButton _growButton;
  private OverlayOpacityField _opacity;
  private Image _emptyCaret;
  private SettingsColorPicker _picker;
  private OverlayInputDock _dock;
  private OverlayGrowPopover _growPopover;
  private SettingsCapsuleGraphic _growMark;
  private TextOverlayElement _element;
  private Action<bool> _closed;
  private float _scale = 1;
  private float _elementScale = 1;
  private float _bottomTop;
  private OverlayGrowth _growth;

  public OverlayTextEditor(Transform parent, TMP_FontAsset uiFont, Func<bool> reduceMotion, OverlayValues values)
  {
    _parent = parent;
    _uiFont = uiFont;
    _reduceMotion = reduceMotion;
    _values = values;
  }

  public bool IsOpen => _screen != null;

  /// <summary>Keeps the editor above the layout toolbar and handles, which raise themselves.</summary>
  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  public void Open(TextOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    _element = element;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _document = OverlayTextDocument.FromRuns(element.Settings?.Text?.Runs);
    _opening = _document.Clone();
    // Labels from converted texts become words in the game's language.
    _document.ReplaceValues(LegacyLabel);
    _caretIndex = _anchor = _document.Count;
    _pendingColor = null;
    _pendingSize = 0;
    _pendingSizeFormula = "";
    _fieldSlot = null;
    _chipPopover = -1;
    _editing.Deactivate();
    _growth = element.Settings?.GrowFrom ?? OverlayGrowth.Auto;
    _undo.Clear();
    _redo.Clear();
    Build();
    Input.imeCompositionMode = IMECompositionMode.On;
    Render();
  }

  private string LegacyLabel(string key)
  {
    if (!OverlayValueCatalog.TryGet(key, out OverlayValueDefinition value) || !OverlayValueCatalog.IsLegacy(value))
      return null;
    _builder.Clear();
    OverlayTextComposer.AppendValue(_builder, value.Id, _values, true);
    return _builder.ToString();
  }

  private void Build()
  {
    SettingsColorPicker oldPicker = _picker;
    _picker = null;
    oldPicker?.Dispose();
    _screen?.Dispose();
    _selectionRects.Clear();
    _pillRects.Clear();
    _dragValue = null;
    _screen = new OverlayEditorFrame(
      "Text editor",
      _parent,
      _uiFont,
      _reduceMotion,
      L(
        "텍스트 편집 · 값을 눌러 계산 · ⌘/Ctrl+Enter 완료 · Esc 취소",
        "Edit text · Click a value to calculate · ⌘/Ctrl+Enter done · Esc cancel"
      ),
      () => Close(false, true),
      () => Close(true, true)
    );
    _ui = _screen.Ui;
    _frame = _screen.Frame;
    _scale = _screen.Scale;

    // The text is placed in screen pixels at its in-game size.
    RectTransform preview = _ui.Rect("Text", _screen.Stage, 0, 0, 10, 10);
    preview.pivot = new Vector2(.5f, .5f);
    _preview = OverlayTextFactory.AddText(
      preview.gameObject,
      _element.Font,
      _element.Material,
      OverlayElementDefaults.TextSize,
      Horizontal(_element.Alignment)
    );
    _preview.textWrappingMode = TextWrappingModes.NoWrap;
    _preview.overflowMode = TextOverflowModes.Overflow;
    _caret = _ui.Panel("Caret", preview, 0, 0, 2, 10, Color.white, false);
    Anchor(_caret.rectTransform);
    _dropCaret = _ui.Panel("Drop caret", preview, 0, 0, 3, 10, SettingsPalette.SoftAccent, false);
    Anchor(_dropCaret.rectTransform);
    _dropCaret.enabled = false;

    BuildFormatBar();
    _bottomTop = _screen.Height - 40;
    _placeholder = _ui.Text(
      L("텍스트를 입력하세요", "Type your text"),
      _frame,
      0,
      0,
      10,
      28,
      16,
      new Color(1, 1, 1, .38f)
    );
    _placeholder.alignment = TextAlignmentOptions.MidlineLeft;
    _placeholder.textWrappingMode = TextWrappingModes.NoWrap;
    // A caret in front of the hint, like an empty text field.
    _emptyCaret = _ui.Panel("Empty caret", _frame, 0, 0, 2, 22, Color.white, false);
    _picker = new SettingsColorPicker(_frame, _uiFont, _reduceMotion);
    _picker.Hidden = PickerHidden;
    _opacity = new OverlayOpacityField(_ui, _picker, () => ActivateField(OpacityKey), _reduceMotion());
    _dock = new OverlayInputDock(
      _ui,
      _frame,
      _screen.Width,
      _screen.Height,
      _screen.Measure,
      _reduceMotion,
      KeypadPress,
      ClearFormula,
      FinishEditing,
      PickValue,
      BeginValueDrag,
      MoveValueDrag,
      EndValueDrag
    );
    // The color picker opens right above the keypad.
    _picker.LayOutAsColumn(_dock.KeypadArea.width);
    _growPopover = new OverlayGrowPopover(_ui, _frame, _reduceMotion(), PickGrowth);
    _growMark = _ui.Capsule("Grow point", _frame, 0, 0, 12, 12, SettingsPalette.SoftAccent);
    _growMark.gameObject.SetActive(false);
  }

  // One row of labeled buttons: the size, the color (with its opacity) and
  // where the text grows from. Size and color act on the selection, or on the
  // whole text when nothing is selected.
  private void BuildFormatBar()
  {
    _formatSurface = _ui.Capsule("Format", _frame, 0, 0, 10, FormatHeight, OverlayEditorUi.Bar);
    _formatSurface.raycastTarget = true;
    _formatBar = _formatSurface.rectTransform;
    _sizeButton = new OverlayBarButton(
      _ui,
      _formatBar,
      "Text size",
      L("크기", "Size"),
      () => ActivateField(SizeKey),
      _reduceMotion(),
      "px",
      glyph: L("가", "Aa")
    );
    _colorButton = new OverlayBarButton(
      _ui,
      _formatBar,
      "Text color",
      L("글자 색", "Text color"),
      OpenPicker,
      _reduceMotion(),
      swatch: true
    );
    _growButton = new OverlayBarButton(
      _ui,
      _formatBar,
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      _reduceMotion(),
      icon: LayoutAction.GrowFrom
    );
  }

  // Values keep their meaning's color when inserted: judgment counts their
  // judgment's color, plain counts white, the rest the highlight color.
  private static OverlayColorSettings InsertColor(OverlayValueId id) =>
    id switch
    {
      OverlayValueId.TooEarly or OverlayValueId.TooLate => OverlayColorSettings.Custom("#FF0000"),
      OverlayValueId.VeryEarly or OverlayValueId.VeryLate => OverlayColorSettings.Custom("#FF6F4E"),
      OverlayValueId.EarlyPerfect or OverlayValueId.LatePerfect => OverlayColorSettings.Custom("#A0FF4E"),
      OverlayValueId.PerfectEarly or OverlayValueId.PurePerfect or OverlayValueId.PerfectLate =>
        OverlayColorSettings.Custom("#" + JudgementColors.Perfect),
      OverlayValueId.Overload or OverlayValueId.Miss => OverlayColorSettings.Custom(
        OverlayElementDefaults.JudgementBase
      ),
      OverlayValueId.SongTitle or OverlayValueId.Attempt or OverlayValueId.FullAttempt or OverlayValueId.TimingScale =>
        OverlayColorSettings.Custom("#FFFFFF"),
      _ => OverlayColorSettings.HighlightColor(),
    };

  // The selection, or the whole text when nothing is selected.
  private void Range(out int start, out int end)
  {
    start = HasSelection ? Mathf.Min(_anchor, _caretIndex) : 0;
    end = HasSelection ? Mathf.Max(_anchor, _caretIndex) : _document.Count;
  }

  private OverlayColorSettings RangeColor()
  {
    Range(out int start, out int end);
    return start < end ? _document[start].Color : TypingColor(_caretIndex);
  }

  private void OpenPicker()
  {
    ClosePopovers();
    if (_picker.Visible)
    {
      _picker.Hide();
      Render();
      return;
    }
    Range(out int start, out int end);
    OverlayColorSettings current = RangeColor();
    _picker.ShowSource(
      HasSelection ? L("선택한 글자 색", "Selected text color") : L("글자 색", "Text color"),
      current,
      color =>
      {
        if (!_pickerSnapshotTaken)
        {
          Remember(false);
          _pickerSnapshotTaken = true;
        }
        OverlayColorSettings before = RangeColor();
        color = _opacity.Keep(before, color);
        if (start < end)
          _document.SetColor(start, end, color);
        else
          _pendingColor = color;
        // A moved slider replaces the opacity the dock is calculating.
        if (_fieldSlot == OpacityKey && !OverlayOpacityField.Read(before).SameAs(OverlayOpacityField.Read(color)))
          _editing.Deactivate();
        Render();
      }
    );
    Render();
  }

  // The color button lets go of its lit state once the picker closes.
  private void PickerHidden()
  {
    _pickerSnapshotTaken = false;
    if (_screen == null || _picker == null)
      return;
    // Its opacity goes with it.
    if (_fieldSlot == OpacityKey)
      ClosePopovers();
    Render();
  }

  private void PlacePicker()
  {
    if (_dock != null)
      OverlayEditorUi.PlaceAboveKeypad(_picker, _dock);
  }

  /// <summary>Done writes the runs to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && _picker.Visible && !_picker.Commit())
      return;
    ClosePopovers();
    TextOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null && !_document.SameAs(_opening))
    {
      element.Settings.Text ??= new OverlayTextSettings();
      element.Settings.Text.Runs = _document.ToRuns();
      element.Apply();
    }
    // Where it grows from is part of its placement, saved with the text.
    if (commit && element?.Settings != null)
      element.SetGrowFrom(_growth);
    Input.imeCompositionMode = IMECompositionMode.Auto;
    SettingsColorPicker picker = _picker;
    _picker = null;
    picker?.Dispose();
    _screen.Dispose();
    _screen = null;
    _ui = null;
    _dragValue = null;
    _editing.Deactivate();
    _element = null;
    _closed = null;
    UIInteractionState.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  public void Dispose() => Close(false, false);

  private static TextAlignmentOptions Horizontal(TextAlignmentOptions alignment)
  {
    int value = (int)alignment & 0xFF;
    return value switch
    {
      (int)HorizontalAlignmentOptions.Left => TextAlignmentOptions.MidlineLeft,
      (int)HorizontalAlignmentOptions.Right => TextAlignmentOptions.MidlineRight,
      _ => TextAlignmentOptions.Midline,
    };
  }

  private static void Anchor(RectTransform rect)
  {
    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
    rect.pivot = Vector2.zero;
  }

  // Screen pixels per reference unit of the overlay canvas.
  private static float ScreenScale => OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
}
