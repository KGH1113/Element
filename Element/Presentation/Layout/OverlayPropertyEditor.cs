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
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// The editing screen for an element another mod draws, built from the parts
/// the shape and text editors use. The mod says where its properties go
/// (<see cref="EditorLayout"/>): buttons in the bar above the element, and
/// numbers under or beside it. Everything else is Element's: the dim, Cancel
/// and Done, the input dock with values and the keypad, the color picker,
/// Grow from, undo, and Esc that saves.
/// </summary>
internal sealed partial class OverlayPropertyEditor : IDisposable
{
  private const float BarHeight = 40,
    MeasureGap = 22;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _font;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayValues _values;
  private readonly Func<OverlayFonts> _fonts;
  private readonly OverlayLiveValues _live;
  private readonly OverlayFormulaEditing _editing = new();
  private readonly OverlayGlide _space = new();
  private readonly ModProps _props = new();
  private readonly List<OverlayModSettings> _undo = new();
  private readonly List<OverlayModSettings> _redo = new();
  private readonly List<(Property Property, OverlayBarButton Button)> _barButtons = new();
  private readonly Event _event = new();
  private OverlayEditorFrame _screen;
  private SettingsUiFactory _ui;
  private RectTransform _frame;
  private RectTransform _preview;
  private ModSurface _surface;
  private RectTransform _bar;
  private OverlayBarButton _growButton;
  private OverlayFormulaField _belowField,
    _besideField;
  private OverlayInputDock _dock;
  private OverlayGrowPopover _growPopover;
  private OverlayChoicePopover _choicePopover;
  private SettingsColorPicker _picker;
  private OverlayOpacityField _opacity;
  private OverlayFormulaField _position;
  private SettingsCapsuleGraphic _growMark;
  private ModOverlayElement _element;
  private ElementType _type;
  private Action<bool> _closed;
  private OverlayModSettings _working;
  private OverlayModSettings _opening;
  private OverlayGrowth _growth;
  private ColorProperty _pickedColor;
  private float _elementScale = 1;
  private float _blinkStart;
  private float _lastAt = -1;
  private string _field;
  private string _lastKey;
  private bool _pickerSnapshotTaken;
  private int _typeRevision;
  private bool _drawFailed;
  private OverlayValueDefinition _dragValue;

  public OverlayPropertyEditor(
    Transform parent,
    TMP_FontAsset font,
    Func<bool> reduceMotion,
    OverlayValues values,
    Func<OverlayFonts> fonts
  )
  {
    _parent = parent;
    _font = font;
    _reduceMotion = reduceMotion;
    _values = values;
    _fonts = fonts;
    _live = new OverlayLiveValues(values);
  }

  public bool IsOpen => _screen != null;

  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  /// <summary>Opens <paramref name="element"/>; false when its mod can't draw it now.</summary>
  public bool Open(ModOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    if (element?.Type == null || element.Settings == null)
      return false;
    _element = element;
    _type = element.Type;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _working = (element.Settings.Mod ?? new OverlayModSettings()).Clone();
    _opening = _working.Clone();
    _growth = element.Settings.GrowFrom;
    _undo.Clear();
    _redo.Clear();
    _field = null;
    _lastKey = null;
    _pickedColor = null;
    _drawFailed = false;
    _editing.Deactivate();
    Build();
    Render();
    return true;
  }

  private string TypeName => L(_type.KoreanName, _type.EnglishName);

  private EditorLayout Layout => _type.Layout ?? new EditorLayout();

  private void Build()
  {
    SettingsColorPicker oldPicker = _picker;
    _picker = null;
    oldPicker?.Dispose();
    _surface?.Dispose();
    _screen?.Dispose();
    _barButtons.Clear();
    _dragValue = null;
    _space.Snap();
    _screen = new OverlayEditorFrame(
      "Mod element editor",
      _parent,
      _font,
      _reduceMotion,
      L(
        TypeName + " 편집 · 숫자를 눌러 입력 · ⌘/Ctrl+Enter나 Esc로 완료",
        "Edit " + TypeName + " · Click a number to type · ⌘/Ctrl+Enter or Esc to finish"
      ),
      () => Close(false, true),
      () => Close(true, true)
    );
    _ui = _screen.Ui;
    _frame = _screen.Frame;

    _preview = _ui.Rect("Preview", _screen.Stage, 0, 0, 10, 10);
    _surface = new ModSurface(_preview, _fonts?.Invoke());
    EditorLayout layout = Layout;
    _belowField = layout.BelowItem == null ? null : Field(layout.BelowItem);
    _besideField = layout.BesideItem == null ? null : Field(layout.BesideItem);

    BuildBar(layout);
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
    _picker = new SettingsColorPicker(_frame, _font, _reduceMotion);
    _picker.Hidden = PickerHidden;
    _picker.LayOutAsColumn(_dock.KeypadArea.width);
    _opacity = new OverlayOpacityField(_ui, _picker, () => Activate(OpacityKey), _reduceMotion());
    BuildGradientPicker();
    _growPopover = new OverlayGrowPopover(_ui, _frame, _reduceMotion(), PickGrowth);
    _choicePopover = new OverlayChoicePopover(_ui, _frame, _reduceMotion(), PickChoice);
    _growMark = _ui.Capsule("Grow point", _frame, 0, 0, 12, 12, SettingsPalette.SoftAccent);
    _growMark.gameObject.SetActive(false);
  }

  private OverlayFormulaField Field(NumberProperty property) =>
    new(
      _ui,
      _frame,
      Name(property),
      Unit(property),
      () => Activate(NumberKey(property)),
      _reduceMotion(),
      Name(property)
    );

  // The mod's properties left to right, at most what fits, then Grow from.
  private void BuildBar(EditorLayout layout)
  {
    SettingsCapsuleGraphic surface = _ui.Capsule("Element bar", _frame, 0, 0, 10, BarHeight, OverlayEditorUi.Bar);
    surface.raycastTarget = true;
    _bar = surface.rectTransform;
    bool motion = _reduceMotion();
    int shown = 0;
    foreach (Property property in layout.BarItems)
    {
      if (shown++ >= EditorLayout.MaximumBar)
        break;
      Property item = property;
      OverlayBarButton button = item switch
      {
        ColorProperty color => new OverlayBarButton(
          _ui,
          _bar,
          "Color " + item.Key,
          Name(item),
          () => OpenColor(color),
          motion,
          swatch: true
        ),
        NumberProperty number => new OverlayBarButton(
          _ui,
          _bar,
          "Number " + item.Key,
          Name(item),
          () => Activate(NumberKey(number)),
          motion,
          Unit(number)
        ),
        ChoiceProperty choice => new OverlayBarButton(
          _ui,
          _bar,
          "Choice " + item.Key,
          Name(item),
          () => ToggleChoice(choice),
          motion
        ),
        ToggleProperty toggle => new OverlayBarButton(
          _ui,
          _bar,
          "Toggle " + item.Key,
          Name(item),
          () => Flip(toggle),
          motion
        ),
        _ => null,
      };
      if (button != null)
        _barButtons.Add((item, button));
    }
    _growButton = new OverlayBarButton(
      _ui,
      _bar,
      "Grow from",
      L("기준점", "Grow from"),
      ToggleGrow,
      motion,
      icon: LayoutAction.GrowFrom
    );
  }

  /// <summary>Done writes the properties and where the element grows from; Cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && _picker.Visible && !_picker.Commit())
      return;
    CloseEditing();
    ModOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null)
    {
      if (!_working.SameAs(_opening))
      {
        element.Settings.Mod = _working.Clone();
        element.Apply();
      }
      element.SetGrowFrom(_growth);
    }
    SettingsColorPicker picker = _picker;
    _picker = null;
    picker?.Dispose();
    _choicePopover?.Hide();
    _surface?.Dispose();
    _surface = null;
    _screen.Dispose();
    _screen = null;
    _ui = null;
    _dragValue = null;
    _editing.Deactivate();
    _element = null;
    _type = null;
    _closed = null;
    UIInteractionState.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  // Torn down while open (the mod or Element turned off), the edit is kept.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }

  private static string Name(Property property) => L(property.KoreanName, property.EnglishName);

  private static string Unit(NumberProperty property) =>
    property.Unit switch
    {
      NumberUnit.Pixels => "px",
      NumberUnit.Percent => "%",
      NumberUnit.Degrees => "°",
      _ => "",
    };

  // Screen pixels per reference unit of the overlay canvas.
  private static float ScreenScale => OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
}
