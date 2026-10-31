using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

/// <summary>
/// Text editing over a dimmed screen. Nothing sits on a card: the text floats
/// in the middle at the size it has in the game, a size and color bar floats
/// above it, and every value is listed below, grouped by category.
/// </summary>
internal sealed partial class OverlayTextEditor : IDisposable
{
  private const float Margin = 32,
    TopBar = 72,
    ChipHeight = 30,
    ChipGap = 8,
    RowGap = 8,
    CategoryGap = 10,
    FormatWidth = 232,
    FormatHeight = 40,
    MinimumSize = 4,
    MaximumSize = 400,
    FadeSeconds = .16f;

  private readonly Transform _parent;
  private readonly TMP_FontAsset _uiFont;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(256);
  private readonly List<Image> _selectionRects = new();
  private readonly List<Image> _pillRects = new();
  private SettingsUiFactory _ui;
  private GameObject _root;
  private CanvasGroup _fade;
  private RectTransform _frame;
  private TextMeshProUGUI _preview;
  private TextMeshProUGUI _placeholder;
  private TextMeshProUGUI _measure;
  private Image _caret;
  private RectTransform _valuesArea;
  private RectTransform _formatBar;
  private TMP_InputField _sizeInput;
  private Button _colorButton;
  private SettingsCapsuleGraphic _colorSwatch;
  private Image _emptyCaret;
  private SettingsColorPicker _picker;
  private TextOverlayElement _element;
  private Action<bool> _closed;
  private float _scale = 1;
  private float _elementScale = 1;
  private float _openedAt;
  private int _viewWidth,
    _viewHeight;

  public OverlayTextEditor(Transform parent, TMP_FontAsset uiFont, Func<bool> reduceMotion, OverlayValues values)
  {
    _parent = parent;
    _uiFont = uiFont;
    _reduceMotion = reduceMotion;
    _values = values;
  }

  public bool IsOpen => _root != null;

  /// <summary>Keeps the editor above the layout toolbar and handles, which raise themselves.</summary>
  public void Raise() => _root?.transform.SetAsLastSibling();

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
    _undo.Clear();
    _redo.Clear();
    _openedAt = Time.unscaledTime;
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
    _viewWidth = Screen.width;
    _viewHeight = Screen.height;
    _picker?.Dispose();
    _picker = null;
    _ui?.Dispose();
    if (_root != null)
      SettingsUiFactory.Destroy(_root);
    _selectionRects.Clear();
    _pillRects.Clear();
    _ui = new SettingsUiFactory(_uiFont) { ReduceMotion = _reduceMotion };
    _root = _ui.Rect("Text editor", _parent, 0, 0, _viewWidth, _viewHeight).gameObject;
    _fade = _root.AddComponent<CanvasGroup>();
    _fade.alpha = _reduceMotion() ? 1 : 0;
    Image dim = _ui.Panel("Dim", _root.transform, 0, 0, _viewWidth, _viewHeight, new Color(0, 0, 0, .92f), false);
    dim.raycastTarget = true;

    // The text is placed in screen pixels at its in-game size.
    RectTransform preview = _ui.Rect("Text", _root.transform, 0, 0, 10, 10);
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

    _scale = Mathf.Clamp(Mathf.Min(_viewWidth / 1120f, _viewHeight / 760f), .55f, 2f);
    float width = _viewWidth / _scale,
      height = _viewHeight / _scale;
    _frame = _ui.Rect("Controls", _root.transform, 0, 0, width, height);
    _frame.localScale = Vector3.one * _scale;
    _measure = _ui.Text("", _frame, 0, 0, 10, 10, 13);
    _measure.fontSize = 13;
    _measure.textWrappingMode = TextWrappingModes.NoWrap;
    _measure.color = Color.clear;
    _ui.Text(
      L("텍스트 편집 · ⌘/Ctrl+Enter 완료 · Esc 취소", "Edit text · ⌘/Ctrl+Enter done · Esc cancel"),
      _frame,
      Margin,
      26,
      width - Margin * 2 - 230,
      28,
      13,
      SettingsUiFactory.Muted
    );
    Button cancel = _ui.Button(
      L("취소", "Cancel"),
      _frame,
      width - Margin - 212,
      20,
      100,
      38,
      () => Close(false, true)
    );
    cancel.name = "Cancel text";
    Button done = _ui.Button(
      L("완료", "Done"),
      _frame,
      width - Margin - 104,
      20,
      104,
      38,
      () => Close(true, true),
      true
    );
    done.name = "Done text";

    BuildFormatBar();
    BuildValues(width, height);
    _placeholder = _ui.Text(
      L("텍스트를 입력하세요", "Type your text"),
      _frame,
      0,
      0,
      width,
      28,
      16,
      new Color(1, 1, 1, .38f)
    );
    _placeholder.alignment = TextAlignmentOptions.MidlineLeft;
    _placeholder.textWrappingMode = TextWrappingModes.NoWrap;
    // A caret in front of the hint, like an empty text field.
    _emptyCaret = _ui.Panel("Empty caret", _frame, 0, 0, 2, 22, Color.white, false);
    _picker = new SettingsColorPicker(_frame, _uiFont, _reduceMotion);
    _picker.Hidden = () => _pickerSnapshotTaken = false;
    _root.transform.SetAsLastSibling();
    EventSystem.current?.SetSelectedGameObject(null);
  }

  // One capsule: text size as − 25px +, then the color. Both act on the
  // selection, or on the whole text when nothing is selected.
  private void BuildFormatBar()
  {
    SettingsCapsuleGraphic pill = _ui.Capsule(
      "Format",
      _frame,
      0,
      0,
      FormatWidth,
      FormatHeight,
      new Color(.13f, .13f, .13f, .96f)
    );
    pill.raycastTarget = true;
    _formatBar = pill.rectTransform;
    TextMeshProUGUI glyph = _ui.Text(L("가", "Aa"), _formatBar, 16, 0, 26, FormatHeight, 15, SettingsUiFactory.Muted);
    glyph.alignment = TextAlignmentOptions.Midline;
    CapsuleButton("Smaller text", "−", 44, 6, 28, 28, () => StepSize(-1));
    _sizeInput = _ui.Input("Text size", "", _formatBar, 74, 0, 58, _ => { }, CommitSize, 5);
    _sizeInput.characterValidation = TMP_InputField.CharacterValidation.Decimal;
    // The number sits in the capsule itself, not in a box of its own.
    foreach (Image surface in _sizeInput.GetComponentsInChildren<Image>(true))
      surface.color = Color.clear;
    // Centered, so the caret after the last digit stays inside the field.
    _sizeInput.textComponent.alignment = TextAlignmentOptions.Center;
    _sizeInput.textComponent.fontSize = 15;
    SettingsUiFactory.Stretch(_sizeInput.textViewport, 2, 4, 22, 4);
    _ui.Text("px", _sizeInput.transform, 38, 0, 20, FormatHeight, 13, SettingsUiFactory.Muted);
    CapsuleButton("Larger text", "+", 134, 6, 28, 28, () => StepSize(1));
    _ui.Panel("Divider", _formatBar, 172, 10, 1, 20, new Color(1, 1, 1, .14f), false);
    _colorButton = CapsuleButton("Text color", "", 184, 4, 32, 32, OpenPicker);
    _ui.Capsule("Ring", _colorButton.transform, 6, 6, 20, 20, new Color(1, 1, 1, .45f));
    _colorSwatch = _ui.Capsule("Swatch", _colorButton.transform, 8, 8, 16, 16, Color.white);
  }

  // A light, see-through capsule button; the dim shows through it.
  private Button CapsuleButton(string name, string text, float x, float y, float width, float height, Action click)
  {
    Transform parent = _formatBar;
    return CapsuleButton(parent, name, text, x, y, width, height, 15, click);
  }

  private Button CapsuleButton(
    Transform parent,
    string name,
    string text,
    float x,
    float y,
    float width,
    float height,
    float fontSize,
    Action click
  )
  {
    SettingsCapsuleGraphic surface = _ui.Capsule(name, parent, x, y, width, height, Color.white);
    surface.raycastTarget = true;
    SettingsButton button = surface.gameObject.AddComponent<SettingsButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = surface;
    button.SetPalette(
      new ColorBlock
      {
        normalColor = new Color(1, 1, 1, parent == _formatBar ? 0 : .07f),
        highlightedColor = new Color(1, 1, 1, .15f),
        pressedColor = new Color(1, 1, 1, .22f),
        selectedColor = new Color(1, 1, 1, parent == _formatBar ? 0 : .07f),
        disabledColor = new Color(1, 1, 1, .03f),
        colorMultiplier = 1,
        fadeDuration = _reduceMotion() ? 0 : .08f,
      }
    );
    SettingsCursor.Attach(button.gameObject, SettingsCursorKind.Pointer);
    button.onClick.AddListener(() => click());
    button.name = name;
    if (text.Length > 0)
    {
      TextMeshProUGUI label = _ui.Text(text, surface.transform, 0, 0, width, height, fontSize);
      label.alignment = TextAlignmentOptions.Midline;
      label.textWrappingMode = TextWrappingModes.NoWrap;
      label.overflowMode = TextOverflowModes.Overflow;
      SettingsUiFactory.Stretch(label.rectTransform, 0, 0, 0, 0);
    }
    return button;
  }

  // Category names in one column on the left; each category's values wrap
  // only within the column to their right.
  private void BuildValues(float width, float height)
  {
    float areaWidth = Mathf.Min(1000, width - Margin * 2);
    float left = (width - areaWidth) * .5f;
    float maxHeight = Mathf.Max(120, height * .4f);
    RectTransform viewport = _ui.Rect("Values", _frame, left, height - maxHeight - 24, areaWidth, maxHeight);
    viewport.gameObject.AddComponent<RectMask2D>();
    Image hit = viewport.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
    scroll.horizontal = false;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.scrollSensitivity = 38;
    scroll.inertia = !_reduceMotion();
    scroll.viewport = viewport;
    RectTransform content = _ui.Rect("Chips", viewport, 0, 0, areaWidth, 10);
    scroll.content = content;
    float column = 0;
    foreach (OverlayValueCategory category in OverlayValueCatalog.Categories)
      column = Mathf.Max(column, Mathf.Ceil(_measure.GetPreferredValues(CategoryName(category)).x));
    column += 28;
    float y = 0;
    foreach (OverlayValueCategory category in OverlayValueCatalog.Categories)
    {
      List<OverlayValueDefinition> values = new();
      foreach (OverlayValueDefinition value in OverlayValueCatalog.All)
        if (value.Category == category)
          values.Add(value);
      if (values.Count == 0)
        continue;
      TextMeshProUGUI name = _ui.Text(
        CategoryName(category),
        content,
        0,
        y,
        column,
        ChipHeight,
        13,
        SettingsUiFactory.Muted
      );
      name.textWrappingMode = TextWrappingModes.NoWrap;
      float x = column;
      foreach (OverlayValueDefinition value in values)
      {
        float chipWidth = Mathf.Ceil(_measure.GetPreferredValues(ValueName(value)).x) + 28;
        if (x > column && x + chipWidth > areaWidth)
        {
          x = column;
          y += ChipHeight + RowGap;
        }
        Button chip = CapsuleButton(
          content,
          "Value " + value.Key,
          ValueName(value),
          x,
          y,
          chipWidth,
          ChipHeight,
          13,
          () => InsertValue(value)
        );
        chip.GetComponentInChildren<TextMeshProUGUI>().color = new Color(1, 1, 1, .92f);
        x += chipWidth + ChipGap;
      }
      y += ChipHeight + RowGap + CategoryGap;
    }
    float contentHeight = Mathf.Max(ChipHeight, y - RowGap - CategoryGap);
    content.sizeDelta = new Vector2(areaWidth, contentHeight);
    float used = Mathf.Min(maxHeight, contentHeight);
    SettingsUiFactory.Place(viewport, left, height - used - 28, areaWidth, used);
    scroll.verticalNormalizedPosition = 1;
    _valuesArea = viewport;
  }

  private static string ValueName(OverlayValueDefinition value) => Korean ? value.KoreanName : value.EnglishName;

  private static string CategoryName(OverlayValueCategory category) =>
    category switch
    {
      OverlayValueCategory.Progress => L("진행", "Progress"),
      OverlayValueCategory.Judgement => L("판정", "Judgment"),
      OverlayValueCategory.Time => L("시간", "Time"),
      OverlayValueCategory.Speed => L("속도", "Speed"),
      OverlayValueCategory.Song => L("곡·기록", "Song"),
      _ => L("시스템", "System"),
    };

  // Values keep their meaning's color when inserted: judgment counts their
  // judgment's color, plain counts white, the rest the highlight color.
  private static OverlayColorSettings InsertColor(OverlayValueId id) =>
    id switch
    {
      OverlayValueId.Judgements => OverlayColorSettings.Custom("#D958FF"),
      OverlayValueId.TooEarly or OverlayValueId.TooLate => OverlayColorSettings.Custom("#FF0000"),
      OverlayValueId.VeryEarly or OverlayValueId.VeryLate => OverlayColorSettings.Custom("#FF6F4E"),
      OverlayValueId.EarlyPerfect or OverlayValueId.LatePerfect => OverlayColorSettings.Custom("#A0FF4E"),
      OverlayValueId.PerfectEarly or OverlayValueId.PurePerfect or OverlayValueId.PerfectLate =>
        OverlayColorSettings.Custom("#" + JudgementColors.Perfect),
      OverlayValueId.Overload or OverlayValueId.Miss => OverlayColorSettings.Custom("#D958FF"),
      OverlayValueId.SongTitle or OverlayValueId.Attempt or OverlayValueId.FullAttempt or OverlayValueId.TimingScale =>
        OverlayColorSettings.Custom("#FFFFFF"),
      _ => OverlayColorSettings.HighlightColor(),
    };

  private void InsertValue(OverlayValueDefinition value)
  {
    if (_picker.Visible)
      _picker.Hide();
    Remember(false);
    int start = Mathf.Min(_anchor, _caretIndex);
    _caretIndex = _anchor = _document.InsertValue(
      start,
      Mathf.Max(_anchor, _caretIndex),
      value.Key,
      InsertColor(value.Id),
      TypingSize(start)
    );
    Changed();
  }

  // The selection, or the whole text when nothing is selected.
  private void Range(out int start, out int end)
  {
    start = HasSelection ? Mathf.Min(_anchor, _caretIndex) : 0;
    end = HasSelection ? Mathf.Max(_anchor, _caretIndex) : _document.Count;
  }

  private void OpenPicker()
  {
    if (_picker.Visible)
    {
      _picker.Hide();
      return;
    }
    Range(out int start, out int end);
    OverlayColorSettings current = start < end ? _document[start].Color : TypingColor(_caretIndex);
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
        if (start < end)
          _document.SetColor(start, end, color);
        else
          _pendingColor = color;
        Render();
      }
    );
    PlacePicker();
  }

  private void CommitSize(string text)
  {
    if (_root == null)
      return;
    if (
      !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float pixels)
      || pixels < MinimumSize
      || pixels > MaximumSize
    )
    {
      ShowSize();
      return;
    }
    Remember(false);
    float size = pixels / _elementScale;
    Range(out int start, out int end);
    if (start < end)
      _document.SetSize(start, end, size);
    else
      _pendingSize = size;
    Render();
  }

  // On the right, clear of the text in the middle, so the colored characters
  // stay visible while the picker is open.
  private void PlacePicker()
  {
    if (_picker == null || !_picker.Visible)
      return;
    float width = _frame.sizeDelta.x,
      height = _frame.sizeDelta.y;
    float x = width - Margin - SettingsColorPicker.Width;
    SettingsUiFactory.Place(
      _picker.Root,
      Mathf.Clamp(x, 8, Mathf.Max(8, width - SettingsColorPicker.Width - 8)),
      Mathf.Clamp(TopBar + 8, 8, Mathf.Max(8, height - _picker.Height - 8)),
      SettingsColorPicker.Width,
      _picker.Height
    );
    _picker.Root.SetAsLastSibling();
  }

  /// <summary>Done writes the runs to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_root == null)
      return;
    if (commit && _picker.Visible && !_picker.Commit())
      return;
    if (commit && _sizeInput.isFocused)
      CommitSize(_sizeInput.text);
    TextOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null && !_document.SameAs(_opening))
    {
      element.Settings.Text ??= new OverlayTextSettings();
      element.Settings.Text.Runs = _document.ToRuns();
      element.Apply();
    }
    Input.imeCompositionMode = IMECompositionMode.Auto;
    _picker?.Dispose();
    _picker = null;
    _ui?.Dispose();
    _ui = null;
    SettingsUiFactory.Destroy(_root);
    _root = null;
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
