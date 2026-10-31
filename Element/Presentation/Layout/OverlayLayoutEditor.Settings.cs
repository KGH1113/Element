using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// A second toolbar that follows the selection with that element's own
// controls: text editing and size for a text, hand or foot for a key viewer,
// kind, fill, border and width for a shape. It drags by its grip like the main
// toolbar and keeps its place; it never blocks the canvas.
internal sealed partial class OverlayLayoutEditor
{
  private const float ElementBarHeight = 56,
    MaximumStroke = 100,
    MinimumTextSize = 4,
    MaximumTextSize = 400;
  private RectTransform _settingsPanel;
  private SettingsUiFactory _settingsUi;
  private IOverlayLayoutTarget _settingsTarget;
  private SettingsSegmentedControl _kindControl;
  private SettingsColorPicker _elementPicker;
  private TMP_InputField _strokeInput;
  private TMP_InputField _textSizeInput;
  private readonly List<RectTransform> _elementBarControls = new();
  private float _settingsWidth,
    _settingsHeight;
  private bool _settingsStale;
  private bool _elementBarPlaced,
    _elementBarDragging;
  private Vector2 _elementBarPosition,
    _elementBarGrab;

  // Shows the toolbar for the selected element, rebuilt when the selection or
  // its settings change; hidden while the element is being dragged.
  private void SyncElementSettings()
  {
    IOverlayLayoutTarget target = HasSelection ? _frames[_selected].Target : null;
    if (target == null || _modalRoot != null || (_textEditor != null && _textEditor.IsOpen))
    {
      CloseElementSettings();
      return;
    }
    if (_settingsTarget != target || _settingsStale)
    {
      _settingsStale = false;
      switch (target)
      {
        case TextOverlayElement text:
          BuildTextSettings(text);
          break;
        case ShapeOverlayElement shape:
          BuildShapeSettings(shape);
          break;
        default:
          if (OverlayElementKinds.IsKeyViewer(target.TemplateId))
            BuildKeyViewerSettings(target);
          else
            CloseElementSettings();
          break;
      }
    }
    if (_settingsPanel == null)
      return;
    bool hidden = (_dragging >= 0 && _pressMoved) || _addMenu != null || (_colorPopover?.Visible ?? false);
    if (_settingsPanel.gameObject.activeSelf == hidden)
    {
      _settingsPanel.gameObject.SetActive(!hidden);
      if (hidden && (_elementPicker?.Visible ?? false))
        _elementPicker.Hide();
    }
    if (_textSizeInput != null && !_textSizeInput.isFocused && target is TextOverlayElement shown)
      _textSizeInput.SetTextWithoutNotify(TextSizeLabel(shown));
    UpdateSettingsPosition();
  }

  private bool SettingsPanelContains(Vector2 point) =>
    _settingsPanel != null && _settingsPanel.gameObject.activeSelf && ElementBarBounds.Contains(point);

  // Above the main toolbar until it is dragged somewhere else.
  private Rect ElementBarBounds
  {
    get
    {
      float width = _settingsWidth * ToolbarScale,
        height = _settingsHeight * ToolbarScale;
      Rect toolbar = ToolbarBounds;
      Vector2 position = _elementBarPlaced
        ? _elementBarPosition
        : new Vector2(toolbar.center.x - width * .5f, toolbar.y - height - 10);
      if (!_elementBarPlaced && position.y < 0)
        position.y = toolbar.yMax + 10;
      position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, ViewWidth - width));
      position.y = Mathf.Clamp(position.y, 0, Mathf.Max(0, ViewHeight - height));
      return new Rect(position, new Vector2(width, height));
    }
  }

  private bool ElementBarControlAt(Vector2 point)
  {
    Vector2 screen = new(point.x, ViewHeight - point.y);
    foreach (RectTransform control in _elementBarControls)
      if (
        control != null
        && control.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint(control, screen, null)
      )
        return true;
    return false;
  }

  private void BeginElementBarDrag(Vector2 pointer)
  {
    _elementBarPosition = ElementBarBounds.position;
    _elementBarPlaced = _elementBarDragging = true;
    _elementBarGrab = pointer - _elementBarPosition;
    if (_elementPicker?.Visible ?? false)
      _elementPicker.Hide();
  }

  private void MoveElementBar(Vector2 pointer)
  {
    _elementBarPosition = pointer - _elementBarGrab;
    _elementBarPosition = ElementBarBounds.position;
    UpdateSettingsPosition();
  }

  private RectTransform BeginSettings(IOverlayLayoutTarget target, float width)
  {
    CloseElementSettings();
    _settingsTarget = target;
    _settingsWidth = width;
    _settingsHeight = ElementBarHeight;
    _settingsUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    Image panel = _settingsUi.Panel(
      "Element toolbar",
      _root.transform,
      0,
      0,
      width,
      ElementBarHeight,
      new Color(.07f, .07f, .07f, 1)
    );
    panel.raycastTarget = true;
    _settingsPanel = panel.rectTransform;
    LayoutActionIcon grip = _settingsUi
      .Rect("Drag element toolbar", _settingsPanel, 10, 16, 14, 24)
      .gameObject.AddComponent<LayoutActionIcon>();
    grip.Kind = LayoutAction.Grip;
    grip.color = SettingsUiFactory.Muted;
    grip.raycastTarget = false;
    return _settingsPanel;
  }

  private T Control<T>(T control)
    where T : Component
  {
    _elementBarControls.Add((RectTransform)control.transform);
    return control;
  }

  private void Divider(float x) =>
    _settingsUi.Panel("Divider", _settingsPanel, x, 16, 1, 24, new Color(1, 1, 1, .1f), false);

  // − [number] px +, committing on Enter or when the field loses focus.
  private TMP_InputField Stepper(float x, string name, string value, Action<string> commit, Action<int> step)
  {
    Button minus = Control(_settingsUi.Button("−", _settingsPanel, x, 8, 32, 40, () => step(-1)));
    minus.name = name + " smaller";
    // Wide enough that the caret after "37.5" or "400" stays inside the field.
    TMP_InputField input = Control(_settingsUi.Input(name, value, _settingsPanel, x + 36, 8, 76, _ => { }, commit, 5));
    input.characterValidation = TMP_InputField.CharacterValidation.Decimal;
    input.gameObject.AddComponent<LayoutTextCapture>();
    SettingsUiFactory.Stretch(input.textViewport, 8, 4, 24, 4);
    _settingsUi.Text("px", input.transform, 54, 0, 20, 40, 12, SettingsUiFactory.Muted);
    Button plus = Control(_settingsUi.Button("+", _settingsPanel, x + 116, 8, 32, 40, () => step(1)));
    plus.name = name + " larger";
    foreach (Button button in new[] { minus, plus })
    {
      // A single symbol needs the whole button, not the text padding.
      TextMeshProUGUI symbol = button.GetComponentInChildren<TextMeshProUGUI>();
      SettingsUiFactory.Stretch(symbol.rectTransform, 0, 0, 0, 0);
      symbol.fontSize = 18;
    }
    return input;
  }

  private void BuildTextSettings(TextOverlayElement text)
  {
    RectTransform panel = BeginSettings(text, 372);
    Button edit = Control(
      _settingsUi.Button(L("텍스트 편집", "Edit text"), panel, 32, 8, 132, 40, () => OpenTextEditor(text, false), true)
    );
    edit.name = "Edit text";
    Divider(176);
    TextMeshProUGUI glyph = _settingsUi.Text(L("가", "Aa"), panel, 188, 8, 24, 40, 15, SettingsUiFactory.Muted);
    glyph.alignment = TextAlignmentOptions.Midline;
    _textSizeInput = Stepper(
      212,
      "Text size",
      TextSizeLabel(text),
      value => CommitTextSize(text, value),
      direction => StepTextSize(text, direction)
    );
  }

  // The whole text's size in pixels as the game draws it; empty when mixed.
  private static string TextSizeLabel(TextOverlayElement text)
  {
    List<OverlayTextRun> runs = text.Settings?.Text?.Runs;
    float size = runs != null && runs.Count > 0 ? runs[0].Size : 0;
    if (runs != null)
      foreach (OverlayTextRun run in runs)
        if (run.Size != size)
          return "";
    float pixels = (size > 0 ? size : OverlayElementDefaults.TextSize) * text.EffectiveScale;
    return pixels.ToString("0.#", CultureInfo.InvariantCulture);
  }

  private void StepTextSize(TextOverlayElement text, int direction)
  {
    List<OverlayTextRun> runs = text.Settings?.Text?.Runs;
    float size = runs != null && runs.Count > 0 && runs[0].Size > 0 ? runs[0].Size : OverlayElementDefaults.TextSize;
    float pixels = Mathf.Round(size * text.EffectiveScale) + direction;
    CommitTextSize(text, Mathf.Clamp(pixels, MinimumTextSize, MaximumTextSize).ToString(CultureInfo.InvariantCulture));
  }

  private void CommitTextSize(TextOverlayElement text, string value)
  {
    if (_closing || text.Settings?.Text == null)
      return;
    if (
      !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float pixels)
      || pixels < MinimumTextSize
      || pixels > MaximumTextSize
    )
    {
      _textSizeInput?.SetTextWithoutNotify(TextSizeLabel(text));
      if (value.Length > 0)
        ShowEditorError(L("글자 크기는 4–400px로 입력하세요", "Enter a text size from 4 to 400px"));
      return;
    }
    float size = pixels / Mathf.Max(.01f, text.EffectiveScale);
    foreach (OverlayTextRun run in text.Settings.Text.Runs)
      run.Size = size;
    text.Apply();
    _textSizeInput?.SetTextWithoutNotify(TextSizeLabel(text));
    _history.Commit(_host.LayoutSettings);
  }

  // Errors share the toolbar's help line, where scale errors already appear.
  private void ShowEditorError(string message)
  {
    _keyboardHelp.text = message;
    _scaleErrorUntil = Time.unscaledTime + 5;
  }

  private void BuildKeyViewerSettings(IOverlayLayoutTarget target)
  {
    RectTransform panel = BeginSettings(target, 216);
    _kindControl = new SettingsSegmentedControl(
      _settingsUi,
      panel,
      32,
      8,
      172,
      40,
      new[] { L("손", "Hand"), L("발", "Foot") },
      index => SwitchKeyViewer(index == 0 ? OverlayElementKinds.HandKeyViewer : OverlayElementKinds.FootKeyViewer)
    );
    _elementBarControls.Add(_kindControl.Root);
    _kindControl.Selected = target.TemplateId == OverlayElementKinds.FootKeyViewer ? 1 : 0;
  }

  private void SwitchKeyViewer(string templateId)
  {
    IOverlayLayoutTarget old = _settingsTarget;
    if (old == null || old.TemplateId == templateId)
      return;
    IOverlayLayoutTarget created = _keyViewers?.Switch(old, templateId);
    if (created == null)
    {
      _kindControl.Selected = old.TemplateId == OverlayElementKinds.FootKeyViewer ? 1 : 0;
      ShowEditorError(
        L(
          "키뷰어를 바꾸지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't switch the key viewer. Check that ElementNote is open."
        )
      );
      return;
    }
    (old as IExternalLayoutOutline)?.SetSelected(false);
    created.SetPreview(true);
    if (IndexOf(created) < 0)
      AddFrame(created);
    _selected = IndexOf(created);
    _settingsTarget = created;
    _history.Commit(_host.LayoutSettings);
    RefreshLayouts();
    UpdateFrames();
  }

  private void BuildShapeSettings(ShapeOverlayElement shape)
  {
    RectTransform panel = BeginSettings(shape, 616);
    _kindControl = new SettingsSegmentedControl(
      _settingsUi,
      panel,
      32,
      8,
      144,
      40,
      new[] { "", "", "" },
      index => ChooseShapeKind(shape, (OverlayShapeKind)index)
    );
    _elementBarControls.Add(_kindControl.Root);
    LayoutAction[] kinds = { LayoutAction.Square, LayoutAction.Triangle, LayoutAction.Circle };
    string[] names = { L("사각형", "Square"), L("삼각형", "Triangle"), L("원", "Circle") };
    for (int i = 0; i < kinds.Length; i++)
    {
      Button segment = _kindControl.Buttons[i];
      segment.name = names[i];
      LayoutActionIcon icon = _settingsUi
        .Rect("Icon", segment.transform, 0, 0, 20, 20)
        .gameObject.AddComponent<LayoutActionIcon>();
      icon.Kind = kinds[i];
      icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
      icon.rectTransform.anchoredPosition = Vector2.zero;
      icon.color = SettingsUiFactory.Foreground;
      icon.raycastTarget = false;
    }
    _kindControl.Selected = (int)shape.Shape.Kind;
    Divider(188);
    _settingsUi.Text(L("채우기", "Fill"), panel, 200, 8, 48, 40, 13, SettingsUiFactory.Muted);
    Control(SwatchButton(250, "Shape fill", () => OpenShapeColor(shape, false)));
    _settingsUi.Text(L("테두리", "Border"), panel, 302, 8, 52, 40, 13, SettingsUiFactory.Muted);
    Control(SwatchButton(356, "Shape border", () => OpenShapeColor(shape, true)));
    Divider(408);
    _settingsUi.Text(L("두께", "Width"), panel, 420, 8, 36, 40, 13, SettingsUiFactory.Muted);
    _strokeInput = Stepper(
      456,
      "Border width",
      shape.Shape.StrokeWidth.ToString("0.#", CultureInfo.InvariantCulture),
      value => CommitStrokeWidth(shape, value),
      direction =>
        CommitStrokeWidth(
          shape,
          Mathf
            .Clamp(Mathf.Round(shape.Shape.StrokeWidth) + direction, 0, MaximumStroke)
            .ToString(CultureInfo.InvariantCulture)
        )
    );
    RefreshShapeSwatches(shape);
  }

  // A color dot in a ring; the picker opens next to the toolbar.
  private Button SwatchButton(float x, string name, Action open)
  {
    Button button = _settingsUi.Button("", _settingsPanel, x, 8, 40, 40, open);
    button.name = name;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _settingsUi.Capsule("Ring", button.transform, 9, 9, 22, 22, new Color(1, 1, 1, .45f));
    _swatchDots[name] = _settingsUi.Capsule("Swatch", button.transform, 11, 11, 18, 18, Color.white);
    return button;
  }

  private readonly Dictionary<string, SettingsCapsuleGraphic> _swatchDots = new();

  private void RefreshShapeSwatches(ShapeOverlayElement shape)
  {
    if (shape.Shape == null)
      return;
    if (_swatchDots.TryGetValue("Shape fill", out SettingsCapsuleGraphic fill) && fill != null)
      fill.color = ShapeGraphic.Resolve(shape.Shape.Fill);
    if (_swatchDots.TryGetValue("Shape border", out SettingsCapsuleGraphic stroke) && stroke != null)
      stroke.color = ShapeGraphic.Resolve(shape.Shape.Stroke);
  }

  private void ChooseShapeKind(ShapeOverlayElement shape, OverlayShapeKind kind)
  {
    if (shape.Shape == null || shape.Shape.Kind == kind)
      return;
    _kindControl.Selected = (int)shape.Shape.Kind;
    if (!OverlayShapeGeometry.IsEdited(shape.Shape))
    {
      SetShapeKind(shape, kind);
      return;
    }
    ClosePopovers();
    OpenConfirmation(
      "Shape kind confirmation",
      L("모양을 바꿀까요?", "Change the shape?"),
      L(
        "꼭짓점을 옮기거나 둥글게 만든 편집이 모두 사라져요.\n크기와 색, 테두리는 그대로예요.\n바꾼 뒤에도 실행 취소할 수 있어요.",
        "Moved and rounded corners will be lost.\nSize, colors and border stay.\nYou can undo the change."
      ),
      L("바꾸기", "Change"),
      () => SetShapeKind(shape, kind),
      () => ChooseShapeKind(shape, kind)
    );
  }

  private void SetShapeKind(ShapeOverlayElement shape, OverlayShapeKind kind)
  {
    OverlayShapeGeometry.SetKind(shape.Shape, kind);
    shape.Apply();
    _history.Commit(_host.LayoutSettings);
    if (_kindControl != null && _settingsTarget == shape)
      _kindControl.Selected = (int)kind;
    UpdateFrames();
  }

  private void OpenShapeColor(ShapeOverlayElement shape, bool stroke)
  {
    if (_elementPicker != null && !_elementPicker.Commit())
      return;
    _valuePopover?.Hide();
    _elementPicker ??= new SettingsColorPicker(_root.transform, _font, () => _preferences.ReduceUiMotion);
    _elementPicker.Hidden = () => _history.Commit(_host.LayoutSettings);
    _elementPicker.ShowSource(
      stroke ? L("테두리 색", "Border color") : L("채우기 색", "Fill color"),
      stroke ? shape.Shape.Stroke : shape.Shape.Fill,
      color =>
      {
        if (stroke)
        {
          shape.Shape.Stroke = color;
          // A visible border needs a width; start from a thin one.
          if (shape.Shape.StrokeWidth <= 0)
          {
            shape.Shape.StrokeWidth = 4;
            _strokeInput?.SetTextWithoutNotify("4");
          }
        }
        else
          shape.Shape.Fill = color;
        shape.Redraw();
        RefreshShapeSwatches(shape);
      }
    );
    UpdateSettingsPosition();
  }

  private void CommitStrokeWidth(ShapeOverlayElement shape, string value)
  {
    if (_closing || shape.Shape == null)
      return;
    if (
      !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float width)
      || float.IsNaN(width)
      || width < 0
      || width > MaximumStroke
    )
    {
      _strokeInput?.SetTextWithoutNotify(shape.Shape.StrokeWidth.ToString("0.#", CultureInfo.InvariantCulture));
      ShowEditorError(L("테두리 두께는 0–100px로 입력하세요", "Enter a border width from 0 to 100px"));
      return;
    }
    shape.Shape.StrokeWidth = width;
    _strokeInput?.SetTextWithoutNotify(width.ToString("0.#", CultureInfo.InvariantCulture));
    shape.Redraw();
    _history.Commit(_host.LayoutSettings);
  }

  private void UpdateSettingsPosition()
  {
    if (_settingsPanel == null)
    {
      if (_elementPicker?.Visible ?? false)
        _elementPicker.Hide();
      return;
    }
    Rect bar = ElementBarBounds;
    PlacePopover(_settingsPanel, bar, _settingsWidth, _settingsHeight);
    if (!(_elementPicker?.Visible ?? false))
      return;
    // Above the toolbar when there is room, otherwise below it.
    float scale = PopupScale(SettingsColorPicker.Width, _elementPicker.Height);
    float w = SettingsColorPicker.Width * scale,
      h = _elementPicker.Height * scale;
    float y = bar.y - h - 10;
    if (y < 8)
      y = bar.yMax + 10;
    Rect picker = new(
      Mathf.Clamp(bar.x, 8, Mathf.Max(8, ViewWidth - w - 8)),
      Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
      w,
      h
    );
    PlacePopover(_elementPicker.Root, picker, SettingsColorPicker.Width, _elementPicker.Height);
  }

  private void CloseElementSettings()
  {
    if (_elementPicker?.Visible ?? false)
      _elementPicker.Hide();
    _elementBarDragging = false;
    if (_settingsPanel == null)
      return;
    if (_strokeInput != null && _strokeInput.isFocused && _settingsTarget is ShapeOverlayElement shape)
      CommitStrokeWidth(shape, _strokeInput.text);
    if (_textSizeInput != null && _textSizeInput.isFocused && _settingsTarget is TextOverlayElement text)
      CommitTextSize(text, _textSizeInput.text);
    _settingsPanel.gameObject.SetActive(false);
    SettingsUiFactory.Destroy(_settingsPanel.gameObject);
    _settingsPanel = null;
    _settingsUi?.Dispose();
    _settingsUi = null;
    _settingsTarget = null;
    _kindControl = null;
    _strokeInput = null;
    _textSizeInput = null;
    _swatchDots.Clear();
    _elementBarControls.Clear();
  }
}
