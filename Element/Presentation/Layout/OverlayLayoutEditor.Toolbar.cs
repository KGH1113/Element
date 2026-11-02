using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;
using Object = UnityEngine.Object;

namespace Element.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor
{
  private readonly ElementSettings _preferences;
  private readonly LayoutHistory _history = new();
  private readonly List<LayoutBounds> _snapReferences = new();
  private readonly List<RectTransform> _toolbarControls = new();
  private readonly List<LayoutActionIcon> _actionIcons = new();
  private SettingsUiFactory _ui;
  private GameObject _ownedEvents;
  private TMP_InputField _scaleInput;
  private TextMeshProUGUI _scaleSuffix;
  private float _scaleErrorUntil;
  private bool _closing,
    _scalePending,
    _toolbarDragging,
    _toolbarPlaced;
  private Vector2 _toolbarPosition,
    _toolbarGrabOffset;
  private string _actionHint = "";
  private int _scaleSelection = -2;
  private Button _editContent;
  private Button _undo,
    _redo,
    _cancel,
    _save,
    _minus,
    _plus,
    _reset,
    _snap,
    _delete,
    _add,
    _editColor;
  private Image _colorSwatch;
  private RectTransform _verticalGuide,
    _horizontalGuide;
  private float _guideX = float.NaN,
    _guideY = float.NaN;
  private LayoutSpacingGuide _horizontalSpacing,
    _verticalSpacing;
  private LayoutSpacingGraphic _spacingGraphic;
  private Rect _lastToolbar;
  private float _lastScale = -1;
  private int _viewportWidth,
    _viewportHeight;
  private int ViewWidth => _viewportWidth > 0 ? _viewportWidth : Screen.width;
  private int ViewHeight => _viewportHeight > 0 ? _viewportHeight : Screen.height;
  private float UiScale => Mathf.Clamp(ViewHeight / 1000f, 1f, 2f);
  private float ToolbarScale => Mathf.Min(UiScale, Mathf.Max(.4f, (ViewWidth - 16) / 640f));

  internal void Resize(int width, int height)
  {
    bool changed = _viewportWidth != width || _viewportHeight != height;
    _viewportWidth = width;
    _viewportHeight = height;
    if (changed)
    {
      // Never capture coordinates from a gesture that began in another viewport.
      if (_dragging >= 0)
      {
        _dragging = -1;
        RefreshLayouts();
      }
      _guideX = _guideY = float.NaN;
      _horizontalSpacing = _verticalSpacing = default;
    }
    UpdateFrames();
  }

  public Rect ToolbarBounds
  {
    get
    {
      float width = 640 * ToolbarScale;
      float height = 120 * ToolbarScale;
      Vector2 p = _toolbarPlaced ? _toolbarPosition : new Vector2((ViewWidth - width) * .5f, ViewHeight - height - 12);
      p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, ViewWidth - width));
      p.y = Mathf.Clamp(p.y, 0, Mathf.Max(0, ViewHeight - height));
      return new Rect(p, new Vector2(width, height));
    }
  }

  private void BeginToolbarDrag(Vector2 pointer)
  {
    _toolbarPosition = ToolbarBounds.position;
    _toolbarDragging = _toolbarPlaced = true;
    _toolbarGrabOffset = pointer - _toolbarPosition;
  }

  private void MoveToolbar(Vector2 pointer)
  {
    _toolbarPosition = pointer - _toolbarGrabOffset;
    _toolbarPosition = ToolbarBounds.position;
  }

  private bool ToolbarControlAt(Vector2 pointer)
  {
    Vector2 local = (pointer - ToolbarBounds.position) / ToolbarScale;
    foreach (RectTransform control in _toolbarControls)
    {
      Rect rect = new(
        control.anchoredPosition.x,
        -control.anchoredPosition.y,
        control.sizeDelta.x,
        control.sizeDelta.y
      );
      if (rect.Contains(local))
        return true;
    }
    return false;
  }

  private void EnsureEvents()
  {
    if (EventSystem.current != null)
      return;
    _ownedEvents = new GameObject("Element Layout Events", typeof(EventSystem), typeof(StandaloneInputModule));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_ownedEvents);
  }

  private void EnsureRoot()
  {
    if (_root != null)
      return;
    _ui = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    _root = new GameObject("Element Layout Editor", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 32760;
    _root.AddComponent<GraphicRaycaster>();
    Image blocker = _ui.Panel("Block game clicks", _root.transform, 0, 0, 0, 0, Color.clear, false);
    SettingsUiFactory.Stretch(blocker.rectTransform, 0, 0, 0, 0);
    blocker.raycastTarget = true;
    _verticalGuide = _ui.Panel(
      "Vertical alignment",
      _root.transform,
      0,
      0,
      1,
      Screen.height,
      new Color(1, 1, 1, .7f),
      false
    ).rectTransform;
    _horizontalGuide = _ui.Panel(
      "Horizontal alignment",
      _root.transform,
      0,
      0,
      Screen.width,
      1,
      new Color(1, 1, 1, .7f),
      false
    ).rectTransform;
    _spacingGraphic = _ui.Rect("Equal spacing guides", _root.transform, 0, 0, Screen.width, Screen.height)
      .gameObject.AddComponent<LayoutSpacingGraphic>();
    _spacingGraphic.raycastTarget = false;
    SettingsUiFactory.Stretch(_spacingGraphic.rectTransform, 0, 0, 0, 0);
    _toolbar = _ui.Panel(
      "Layout actions",
      _root.transform,
      0,
      0,
      640,
      120,
      new Color(.07f, .07f, .07f, 1)
    ).rectTransform;
    _toolbar.anchorMin = _toolbar.anchorMax = Vector2.zero;
    LayoutActionIcon grip = _ui.Rect("Drag panel", _toolbar, 10, 18, 14, 24)
      .gameObject.AddComponent<LayoutActionIcon>();
    grip.Kind = LayoutAction.Grip;
    grip.color = SettingsUiFactory.Muted;
    grip.raycastTarget = false;
    _instruction = _ui.Text("", _toolbar, 32, 12, 192, 36, 16);
    Button resetAll = _ui.Button(L("전체 초기화", "Reset all"), _toolbar, 232, 12, 104, 36, OpenResetConfirmation);
    _toolbarControls.Add((RectTransform)resetAll.transform);
    _keyboardHelp = _ui.Text("", _toolbar, 12, 102, 616, 14, 11, SettingsUiFactory.Muted);
    _keyboardHelp.alignment = TextAlignmentOptions.Midline;
    _undo = IconButton(LayoutAction.Undo, L("실행 취소", "Undo"), 344, 12, Undo, 36);
    _redo = IconButton(LayoutAction.Redo, L("다시 실행", "Redo"), 384, 12, Redo, 36);
    _cancel = _ui.Button(L("취소", "Cancel"), _toolbar, 428, 12, 88, 36, () => Close(false));
    _save = _ui.Button(L("저장", "Save"), _toolbar, 524, 12, 104, 36, () => Close(true), true);
    _ui.Panel("Divider", _toolbar, 12, 53, 616, 1, new Color(1, 1, 1, .08f), false);
    _plus = IconButton(LayoutAction.Plus, L("확대", "Increase size"), 12, 60, () => ScaleBy(1.05f), 40);
    _scaleInput = _ui.Input(
      "Scale percent",
      "100",
      _toolbar,
      56,
      60,
      84,
      _ =>
      {
        _scalePending = true;
        AlignScaleValue();
      },
      CommitScale,
      5
    );
    _scaleInput.characterValidation = TMP_InputField.CharacterValidation.Decimal;
    _scaleInput.textViewport.anchorMin = _scaleInput.textViewport.anchorMax = new Vector2(0, 1);
    _scaleInput.textViewport.pivot = new Vector2(0, 1);
    _scaleInput.textComponent.alignment = TextAlignmentOptions.Right;
    // Right-aligned text puts the caret on the field's edge; keep room for it.
    _scaleInput.textComponent.margin = new Vector4(0, 0, 2, 0);
    _scaleInput.gameObject.AddComponent<LayoutTextCapture>();
    _scaleSuffix = _ui.Text("%", _scaleInput.transform, 63, 0, 14, 40, 14, SettingsUiFactory.Muted);
    AlignScaleValue();
    _minus = IconButton(LayoutAction.Minus, L("축소", "Decrease size"), 148, 60, () => ScaleBy(.95f), 40);
    // Texts and shapes are sized in their editor; their place for scale opens it.
    _editContent = _ui.Button(L("텍스트 편집", "Edit text"), _toolbar, 12, 60, 172, 40, EditSelected, true);
    _editContent.name = "Edit element";
    _toolbarControls.Add((RectTransform)_editContent.transform);
    _ui.Panel("Selection divider", _toolbar, 196, 68, 1, 24, new Color(1, 1, 1, .1f), false);
    _reset = IconButton(LayoutAction.Reset, L("선택 요소 초기화", "Reset element"), 208, 60, ResetSelected, 40);
    _snap = IconButton(
      LayoutAction.Snap,
      L("자동 맞춤", "Snapping"),
      248,
      60,
      () =>
      {
        _preferences.LayoutSnapping = !_preferences.LayoutSnapping;
        UpdateToolbar();
      },
      40
    );
    _delete = IconButton(
      LayoutAction.Delete,
      L("요소 삭제 · Cmd/Ctrl + Delete", "Delete element · Cmd/Ctrl + Delete"),
      288,
      60,
      DeleteSelected,
      40
    );
    _ui.Panel("Add divider", _toolbar, 336, 68, 1, 24, new Color(1, 1, 1, .1f), false);
    _add = _ui.Button(L("요소 추가", "Add Element"), _toolbar, 348, 60, 132, 40, ToggleAddMenu, true);
    _editColor = _ui.Button(L("하이라이트 색", "Highlight"), _toolbar, 488, 60, 140, 40, ToggleColor);
    _colorSwatch = _ui.Panel("Highlight color", _editColor.transform, 12, 12, 16, 16, _ui.Accent);
    SettingsUiFactory.Stretch(_editColor.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 32, 0, 8, 0);
    _toolbarControls.Add((RectTransform)_add.transform);
    _toolbarControls.Add((RectTransform)_editColor.transform);
    _toolbarControls.Add((RectTransform)_cancel.transform);
    _toolbarControls.Add((RectTransform)_save.transform);
    _toolbarControls.Add((RectTransform)_scaleInput.transform);
    _root.SetActive(false);
  }

  private Button IconButton(LayoutAction kind, string hint, float x, float y, Action onClick, float height = 32)
  {
    Button button = _ui.Button("", _toolbar, x, y, 36, height, onClick);
    button.gameObject.name = hint;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon icon = _ui.Rect("Icon", button.transform, 8, 6, 20, 20)
      .gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
    icon.rectTransform.anchoredPosition = Vector2.zero;
    icon.raycastTarget = false;
    _actionIcons.Add(icon);
    _toolbarControls.Add((RectTransform)button.transform);
    LayoutActionHint tooltip = button.gameObject.AddComponent<LayoutActionHint>();
    tooltip.Changed = visible => _actionHint = visible ? hint : "";
    return button;
  }

  private void EditSelected()
  {
    if (HasSelection)
      OpenContentEditor(_frames[_selected].Target);
  }

  // Texts and shapes take their size from their editor, not from a scale.
  private static bool SizedInEditor(IOverlayLayoutTarget target) =>
    target is TextOverlayElement || target is ShapeOverlayElement;

  private bool HasSelection =>
    _selected >= 0
    && _selected < _frames.Count
    && _frames[_selected].Target.Available
    && _frames[_selected].Target.Enabled;

  private void AlignScaleValue()
  {
    if (_scaleInput == null || _scaleSuffix == null)
      return;
    float width = ((RectTransform)_scaleInput.transform).sizeDelta.x;
    float suffix = _scaleSuffix.GetPreferredValues("%").x;
    float number = Mathf.Clamp(
      _scaleInput.textComponent.GetPreferredValues(_scaleInput.text).x + 2,
      14,
      width - suffix - 18
    );
    float left = (width - number - 2 - suffix) * .5f;
    SettingsUiFactory.Place(_scaleInput.textViewport, left, 4, number, 32);
    SettingsUiFactory.Place(_scaleSuffix.rectTransform, left + number + 2, 0, suffix + 1, 40);
  }

  private void RefreshLayouts()
  {
    _scalePending = false;
    _scaleErrorUntil = 0;
    _host.ApplyLayouts();
    if (_keyViewers != null)
      foreach (IOverlayLayoutTarget target in _keyViewers.Targets)
        target.RefreshLayout();
    PruneSelection();
    _lastScale = -1;
  }

  private void Undo()
  {
    FinishDrag();
    _settingsStale = true;
    _history.Undo(_host.LayoutSettings);
    RefreshLayouts();
  }

  private void Redo()
  {
    FinishDrag();
    _settingsStale = true;
    _history.Redo(_host.LayoutSettings);
    RefreshLayouts();
  }

  private void ResetSelected()
  {
    if (!HasSelection)
      return;
    ClosePopovers();
    foreach (IOverlayLayoutTarget target in _selection)
      target.ResetLayout();
    _scalePending = false;
    _scaleErrorUntil = 0;
    _history.Commit(_host.LayoutSettings);
    _lastScale = -1;
  }

  private void CommitScale(string value)
  {
    if (_closing || !HasSelection)
      return;
    float current = ValidScale(_frames[_selected].Target.EffectiveScale);
    float min = Mathf.Min(20, current * 100);
    float max = Mathf.Max(MaximumScale * 100, current * 100);
    if (
      !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
      || !OverlayLayoutGeometry.Finite(percent)
      || percent < min
      || percent > max
    )
    {
      string range =
        min.ToString("0.#", CultureInfo.InvariantCulture) + "–" + max.ToString("0.#", CultureInfo.InvariantCulture);
      _keyboardHelp.text = L(range + "%로 입력하세요", "Enter " + range + "%");
      _scaleErrorUntil = Time.unscaledTime + 5;
      _helpIsNote = false;
      _lastScale = -1;
      return;
    }
    SetScale(percent / 100f);
  }

  private void ScaleBy(float ratio)
  {
    if (HasSelection)
      SetScale(_frames[_selected].Target.EffectiveScale * ratio);
  }

  private void SetScale(float scale)
  {
    _scalePending = false;
    _scaleErrorUntil = 0;
    IOverlayLayoutTarget target = _frames[_selected].Target;
    float current = ValidScale(target.EffectiveScale);
    target.SetBounds(
      ToRect(
        OverlayLayoutGeometry.ScaleAtCenter(
          ToBounds(target.Bounds),
          Mathf.Clamp(scale, Mathf.Min(.2f, current), Mathf.Max(MaximumScale, current)) / current,
          ViewWidth,
          ViewHeight,
          Mathf.Min(1, .2f / current),
          Mathf.Max(1, MaximumScale / current)
        )
      )
    );
    target.CommitLayout(false);
    _history.Commit(_host.LayoutSettings);
    _lastScale = -1;
  }

  private static float ValidScale(float value) => OverlayLayoutGeometry.Finite(value) && value > 0 ? value : 1;

  private void UpdateToolbar()
  {
    if (_ui == null)
      return;
    Rect bounds = ToolbarBounds;
    if (bounds != _lastToolbar)
    {
      _lastToolbar = bounds;
      SetRect(_toolbar, bounds);
      _toolbar.localScale = Vector3.one * ToolbarScale;
      _toolbar.sizeDelta = bounds.size / ToolbarScale;
      _toolbar.SetAsLastSibling();
    }
    _undo.interactable = _history.CanUndo;
    _redo.interactable = _history.CanRedo;
    IOverlayLayoutTarget selection = HasSelection ? _frames[_selected].Target : null;
    bool several = MultiSelected;
    // Several selected show the scale, unavailable, rather than one element's Edit.
    bool edit = !several && SizedInEditor(selection);
    if (_editContent.gameObject.activeSelf != edit)
    {
      _editContent.gameObject.SetActive(edit);
      _plus.gameObject.SetActive(!edit);
      _minus.gameObject.SetActive(!edit);
      _scaleInput.gameObject.SetActive(!edit);
    }
    if (edit)
      _editContent.GetComponentInChildren<TextMeshProUGUI>().text =
        selection is ShapeOverlayElement ? L("도형 편집", "Edit shape") : L("텍스트 편집", "Edit text");
    _scaleInput.interactable = _minus.interactable = _plus.interactable = HasSelection && !several;
    _reset.interactable = _delete.interactable = HasSelection;
    float scale =
      several ? -2
      : HasSelection ? _frames[_selected].Target.EffectiveScale * 100
      : 100;
    if (_scaleSelection != _selected)
    {
      _scaleSelection = _selected;
      _scalePending = false;
      _scaleErrorUntil = 0;
      _lastScale = -1;
    }
    if (!_scaleInput.isFocused && !_scalePending && scale != _lastScale)
    {
      _scaleInput.SetTextWithoutNotify(several ? "—" : scale.ToString("0.#", CultureInfo.InvariantCulture));
      // A dash on its own: several elements have no one scale.
      _scaleSuffix.gameObject.SetActive(!several);
      AlignScaleValue();
      _lastScale = scale;
    }
    _instruction.text = HasSelection ? SelectionLabel() : L("요소 선택", "Select overlay");
    bool message = Time.unscaledTime < _scaleErrorUntil;
    _keyboardHelp.color =
      !message ? SettingsUiFactory.Muted
      : _helpIsNote ? SettingsUiFactory.Foreground
      : new Color(1, .66f, .53f);
    if (!message)
      _helpIsNote = false;
    if (Time.unscaledTime >= _scaleErrorUntil)
      _keyboardHelp.text = _actionHint;
    if (_themeVersion != Overlay.Shared.OverlayTheme.Version)
    {
      _themeVersion = Overlay.Shared.OverlayTheme.Version;
      _ui.RefreshTheme();
      _colorSwatch.color = _ui.Accent;
    }
    foreach (LayoutActionIcon icon in _actionIcons)
    {
      // The settings button is hidden without a selection.
      if (!icon.gameObject.activeInHierarchy)
        continue;
      bool enabled = icon.GetComponentInParent<Button>().interactable;
      icon.color =
        !enabled ? new Color(.4f, .4f, .4f)
        : icon.Kind == LayoutAction.Snap
          ? (_preferences.LayoutSnapping ? SettingsPalette.SoftAccent : SettingsUiFactory.Muted)
        : SettingsUiFactory.Foreground;
    }
  }

  private void UpdateGuides()
  {
    _spacingGraphic.color = SettingsPalette.SoftAccent;
    _spacingGraphic.SetGuides(_horizontalSpacing, _verticalSpacing, UiScale);
    _verticalGuide.gameObject.SetActive(!float.IsNaN(_guideX));
    _horizontalGuide.gameObject.SetActive(!float.IsNaN(_guideY));
    if (!float.IsNaN(_guideX))
      SettingsUiFactory.Place(_verticalGuide, _guideX, 0, 1, Screen.height);
    if (!float.IsNaN(_guideY))
      SettingsUiFactory.Place(_horizontalGuide, 0, _guideY, Screen.width, 1);
  }
}

internal sealed class LayoutTextCapture : MonoBehaviour, ISelectHandler, IDeselectHandler
{
  public void OnSelect(BaseEventData data) => UIInteractionState.SetTextEditing(true);

  public void OnDeselect(BaseEventData data) => UIInteractionState.SetTextEditing(false);

  private void OnDisable() => UIInteractionState.SetTextEditing(false);
}

internal sealed class LayoutActionHint
  : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
  public Action<bool> Changed;

  public void OnPointerEnter(PointerEventData data) => Changed?.Invoke(true);

  public void OnPointerExit(PointerEventData data) => Changed?.Invoke(false);

  public void OnSelect(BaseEventData data) => Changed?.Invoke(true);

  public void OnDeselect(BaseEventData data) => Changed?.Invoke(false);

  private void OnDisable() => Changed?.Invoke(false);
}
