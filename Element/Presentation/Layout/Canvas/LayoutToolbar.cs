using System;
using System.Collections.Generic;
using System.Globalization;
using Element.Application.Layout;
using Element.Domain.Layout;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The main toolbar at the bottom of the view: what is selected, its scale
/// (or its Edit button when it is sized in its editor), undo, reset, snap,
/// delete, Add Element, the highlight color and the default font, Cancel
/// and Save. It drags from anywhere but its controls and keeps its place.
/// </summary>
internal sealed class LayoutToolbar
{
  private const float Width = LayoutSession.ToolbarWidth;

  private readonly LayoutSession _session;
  private readonly LayoutSelection _selection;
  private readonly LayoutStatusLine _status;
  private readonly ElementKinds _kinds;
  private readonly List<RectTransform> _controls = new();
  private readonly List<LayoutIcon> _icons = new();
  private RectTransform _toolbar;
  private TextMeshProUGUI _instruction;
  private TMP_InputField _scaleInput;
  private TextMeshProUGUI _scaleSuffix;
  private Button _editContent;
  private TextMeshProUGUI _editLabel;
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
    _editColor,
    _editFont;
  private Image _colorSwatch;
  private bool _scalePending,
    _dragging,
    _placed;
  private Vector2 _position,
    _grab;
  private int _scaleSelection = -2;
  private Rect _lastBounds;
  private float _lastScale = -1;
  private int _themeVersion = -1;

  public LayoutToolbar(LayoutSession session, LayoutSelection selection, LayoutStatusLine status, ElementKinds kinds)
  {
    _session = session;
    _selection = selection;
    _status = status;
    _kinds = kinds;
  }

  public RectTransform Root => _toolbar;

  /// <summary>The Add Element button, focused again when its menu closes.</summary>
  public Button AddButton => _add;

  /// <summary>Whether the toolbar is being dragged.</summary>
  public bool Dragging
  {
    get => _dragging;
    set => _dragging = value;
  }

  /// <summary>Whether the scale field has the keyboard.</summary>
  public bool Typing => _scaleInput != null && _scaleInput.isFocused;

  /// <summary>In screen pixels from the top left; centered at the bottom until dragged.</summary>
  public Rect Bounds
  {
    get
    {
      float scale = _session.ToolbarScale;
      float width = Width * scale;
      float height = 120 * scale;
      int viewWidth = _session.ViewWidth,
        viewHeight = _session.ViewHeight;
      Vector2 p = _placed ? _position : new Vector2((viewWidth - width) * .5f, viewHeight - height - 12);
      p.x = Mathf.Clamp(p.x, 0, Mathf.Max(0, viewWidth - width));
      p.y = Mathf.Clamp(p.y, 0, Mathf.Max(0, viewHeight - height));
      return new Rect(p, new Vector2(width, height));
    }
  }

  public void Build(LayoutCommands commands)
  {
    SettingsUiFactory ui = _session.Ui;
    _toolbar = ui.Panel("Layout actions", _session.Root.transform, 0, 0, Width, 120, UiPalette.Toolbar).rectTransform;
    _toolbar.anchorMin = _toolbar.anchorMax = Vector2.zero;
    LayoutActionIcon grip = ui.Rect("Drag panel", _toolbar, 10, 18, 14, 24).gameObject.AddComponent<LayoutActionIcon>();
    grip.Kind = LayoutAction.Grip;
    grip.color = UiPalette.Muted;
    grip.raycastTarget = false;
    _instruction = ui.Text("", _toolbar, 32, 12, 312, 36, 16);
    Button resetAll = ui.Button(L("전체 초기화", "Reset all"), _toolbar, 352, 12, 104, 36, commands.ResetAll);
    _controls.Add((RectTransform)resetAll.transform);
    _status.Build(ui, _toolbar);
    _undo = IconButton(LayoutAction.Undo, L("실행 취소", "Undo"), 464, 12, commands.Undo, 36);
    _redo = IconButton(LayoutAction.Redo, L("다시 실행", "Redo"), 504, 12, commands.Redo, 36);
    _cancel = ui.Button(L("취소", "Cancel"), _toolbar, 548, 12, 88, 36, commands.Cancel);
    _save = ui.Button(L("저장", "Save"), _toolbar, 644, 12, 104, 36, commands.Save, true);
    ui.Panel("Divider", _toolbar, 12, 53, Width - 24, 1, new Color(1, 1, 1, .08f), false);
    _plus = IconButton(LayoutAction.Plus, L("확대", "Increase size"), 12, 60, () => ScaleBy(1.05f), 40);
    _scaleInput = ui.Input(
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
    _scaleSuffix = ui.Text("%", _scaleInput.transform, 63, 0, 14, 40, 14, UiPalette.Muted);
    AlignScaleValue();
    _minus = IconButton(LayoutAction.Minus, L("축소", "Decrease size"), 148, 60, () => ScaleBy(.95f), 40);
    // Elements sized in their editor show its Edit button in the scale's place.
    _editContent = ui.Button(L("텍스트 편집", "Edit text"), _toolbar, 12, 60, 172, 40, commands.Edit, true);
    _editContent.name = "Edit element";
    _editLabel = _editContent.GetComponentInChildren<TextMeshProUGUI>();
    _controls.Add((RectTransform)_editContent.transform);
    ui.Panel("Selection divider", _toolbar, 196, 68, 1, 24, new Color(1, 1, 1, .1f), false);
    _reset = IconButton(
      LayoutAction.Reset,
      L("선택 요소 초기화", "Reset element"),
      208,
      60,
      commands.ResetSelected,
      40
    );
    _snap = IconButton(
      LayoutAction.Snap,
      L("자동 맞춤", "Snapping"),
      248,
      60,
      () =>
      {
        _session.Preferences.LayoutSnapping = !_session.Preferences.LayoutSnapping;
        Refresh();
      },
      40
    );
    _delete = IconButton(
      LayoutAction.Delete,
      L("요소 삭제 · Cmd/Ctrl + Delete", "Delete element · Cmd/Ctrl + Delete"),
      288,
      60,
      commands.Delete,
      40
    );
    ui.Panel("Add divider", _toolbar, 336, 68, 1, 24, new Color(1, 1, 1, .1f), false);
    _add = ui.Button(L("요소 추가", "Add Element"), _toolbar, 348, 60, 132, 40, commands.Add, true);
    _editColor = ui.Button(L("하이라이트 색", "Highlight"), _toolbar, 488, 60, 140, 40, commands.Color);
    _colorSwatch = ui.Panel("Highlight color", _editColor.transform, 12, 12, 16, 16, ui.Accent);
    SettingsUiFactory.Stretch(_editColor.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 32, 0, 8, 0);
    // The texts' default font sits beside their highlight color.
    _editFont = ui.Button(L("기본 폰트", "Font"), _toolbar, 636, 60, 112, 40, commands.Font);
    _editFont.name = "Default font";
    LayoutActionIcon fontIcon = ui.Rect("Icon", _editFont.transform, 10, 10, 20, 20)
      .gameObject.AddComponent<LayoutActionIcon>();
    fontIcon.Kind = LayoutAction.Font;
    fontIcon.raycastTarget = false;
    _icons.Add(new LayoutIcon(fontIcon, _editFont));
    SettingsUiFactory.Stretch(_editFont.GetComponentInChildren<TextMeshProUGUI>().rectTransform, 34, 0, 8, 0);
    _editFont.interactable = _session.Fonts != null;
    _controls.Add((RectTransform)_add.transform);
    _controls.Add((RectTransform)_editColor.transform);
    _controls.Add((RectTransform)_editFont.transform);
    _controls.Add((RectTransform)_cancel.transform);
    _controls.Add((RectTransform)_save.transform);
    _controls.Add((RectTransform)_scaleInput.transform);
  }

  private Button IconButton(LayoutAction kind, string hint, float x, float y, Action onClick, float height = 32)
  {
    SettingsUiFactory ui = _session.Ui;
    Button button = ui.Button("", _toolbar, x, y, 36, height, onClick);
    button.gameObject.name = hint;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon icon = ui.Rect("Icon", button.transform, 8, 6, 20, 20).gameObject.AddComponent<LayoutActionIcon>();
    icon.Kind = kind;
    icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(.5f, .5f);
    icon.rectTransform.anchoredPosition = Vector2.zero;
    icon.raycastTarget = false;
    _icons.Add(new LayoutIcon(icon, button));
    _controls.Add((RectTransform)button.transform);
    button.gameObject.AddComponent<LayoutActionHint>().Changed = _status.HintFor(hint);
    return button;
  }

  /// <summary>
  /// Where a popover of <paramref name="width"/> × <paramref name="height"/>
  /// opens: above the toolbar (below it when there is no room), from
  /// <paramref name="toolbarX"/> along it or flush with its right end, scaled to fit the view.
  /// </summary>
  public Rect PopoverAbove(float width, float height, float toolbarX, bool alignRight)
  {
    Rect toolbar = Bounds;
    float viewWidth = _session.ViewWidth,
      viewHeight = _session.ViewHeight;
    float scale = Mathf.Min(_session.UiScale, Mathf.Min((viewWidth - 16) / width, (viewHeight - 16) / height));
    float w = width * scale,
      h = height * scale;
    float x = alignRight ? toolbar.xMax - w : toolbar.x + toolbarX * _session.ToolbarScale;
    return PopoverPlacement.Above(toolbar, x, w, h, 10, viewWidth, viewHeight);
  }

  public void BeginDrag(Vector2 pointer)
  {
    _position = Bounds.position;
    _dragging = _placed = true;
    _grab = pointer - _position;
  }

  public void Move(Vector2 pointer)
  {
    _position = pointer - _grab;
    _position = Bounds.position;
  }

  /// <summary>Whether a control, which takes the press itself, is under <paramref name="pointer"/>.</summary>
  public bool ControlAt(Vector2 pointer)
  {
    Vector2 local = (pointer - Bounds.position) / _session.ToolbarScale;
    foreach (RectTransform control in _controls)
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

  public void Show(bool shown)
  {
    if (_toolbar.gameObject.activeSelf != shown)
      _toolbar.gameObject.SetActive(shown);
  }

  /// <summary>A scale being typed is dropped; the field shows the element's again once it lets go.</summary>
  public void DropTypedScale() => _scalePending = false;

  /// <summary>The scale shown is read again; a typed scale is dropped.</summary>
  public void ForgetScale()
  {
    _scalePending = false;
    _lastScale = -1;
  }

  /// <summary>
  /// Applies a scale still being typed when the editor saves; false (with
  /// the field focused again) when it can't be used.
  /// </summary>
  public bool CommitPendingScale()
  {
    if (!_selection.HasSelection || !_scalePending)
      return true;
    CommitScale(_scaleInput.text);
    if (_status.Showing)
    {
      _scaleInput.Select();
      return false;
    }
    _scaleInput.DeactivateInputField();
    return true;
  }

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

  private void CommitScale(string value)
  {
    if (_session.Closing || !_selection.HasSelection)
      return;
    float current = LayoutSession.ValidScale(_selection.Primary.EffectiveScale);
    float min = Mathf.Min(20, current * 100);
    float max = Mathf.Max(LayoutSession.MaximumScale * 100, current * 100);
    if (
      !float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
      || !OverlayLayoutGeometry.Finite(percent)
      || percent < min
      || percent > max
    )
    {
      string range =
        min.ToString("0.#", CultureInfo.InvariantCulture) + "–" + max.ToString("0.#", CultureInfo.InvariantCulture);
      _status.ShowError(L(range + "%로 입력하세요", "Enter " + range + "%"));
      _lastScale = -1;
      return;
    }
    SetScale(percent / 100f);
  }

  public void ScaleBy(float ratio)
  {
    if (_selection.HasSelection)
      SetScale(_selection.Primary.EffectiveScale * ratio);
  }

  private void SetScale(float scale)
  {
    _scalePending = false;
    _status.Clear();
    IOverlayLayoutTarget target = _session.Frames[_selection.Selected].Target;
    float current = LayoutSession.ValidScale(target.EffectiveScale);
    float maximum = LayoutSession.MaximumScale;
    target.SetBounds(
      LayoutSession.ToRect(
        OverlayLayoutGeometry.ScaleAtCenter(
          LayoutSession.ToBounds(target.Bounds),
          Mathf.Clamp(scale, Mathf.Min(.2f, current), Mathf.Max(maximum, current)) / current,
          _session.ViewWidth,
          _session.ViewHeight,
          Mathf.Min(1, .2f / current),
          Mathf.Max(1, maximum / current)
        )
      )
    );
    target.CommitLayout(false);
    _session.Commit();
    _lastScale = -1;
  }

  /// <summary>Once a frame: place, what is selected, which controls apply, the status line and icon colors.</summary>
  public void Refresh()
  {
    if (_session.Ui == null)
      return;
    Rect bounds = Bounds;
    if (bounds != _lastBounds)
    {
      _lastBounds = bounds;
      float scale = _session.ToolbarScale;
      _session.SetRect(_toolbar, bounds);
      _toolbar.localScale = Vector3.one * scale;
      _toolbar.sizeDelta = bounds.size / scale;
      _toolbar.SetAsLastSibling();
    }
    _undo.interactable = _session.History.CanUndo;
    _redo.interactable = _session.History.CanRedo;
    bool any = _selection.HasSelection;
    IOverlayLayoutTarget selection = _selection.Primary;
    bool several = _selection.Multi;
    // Several selected show the scale, unavailable, rather than one element's Edit.
    bool edit = !several && _kinds.SizedInEditor(selection);
    if (_editContent.gameObject.activeSelf != edit)
    {
      _editContent.gameObject.SetActive(edit);
      _plus.gameObject.SetActive(!edit);
      _minus.gameObject.SetActive(!edit);
      _scaleInput.gameObject.SetActive(!edit);
    }
    if (edit)
      _editLabel.text = _kinds.EditCaption(selection);
    _scaleInput.interactable = _minus.interactable = _plus.interactable = any && !several;
    _reset.interactable = _delete.interactable = any;
    float shown =
      several ? -2
      : any ? selection.EffectiveScale * 100
      : 100;
    if (_scaleSelection != _selection.Selected)
    {
      _scaleSelection = _selection.Selected;
      _scalePending = false;
      _status.Clear();
      _lastScale = -1;
    }
    if (!_scaleInput.isFocused && !_scalePending && shown != _lastScale)
    {
      _scaleInput.SetTextWithoutNotify(several ? "—" : shown.ToString("0.#", CultureInfo.InvariantCulture));
      // A dash on its own: several elements have no one scale.
      _scaleSuffix.gameObject.SetActive(!several);
      AlignScaleValue();
      _lastScale = shown;
    }
    _instruction.text = any ? SelectionLabel() : L("요소 선택", "Select overlay");
    _status.Refresh();
    if (_themeVersion != UiTheme.Version)
    {
      _themeVersion = UiTheme.Version;
      _session.Ui.RefreshTheme();
      _colorSwatch.color = _session.Ui.Accent;
    }
    foreach (LayoutIcon icon in _icons)
    {
      // The settings button is hidden without a selection.
      if (!icon.Icon.gameObject.activeInHierarchy)
        continue;
      icon.Icon.color =
        !icon.Button.interactable ? UiPalette.Disabled
        : icon.Icon.Kind == LayoutAction.Snap
          ? (_session.Preferences.LayoutSnapping ? UiPalette.SoftAccent : UiPalette.Muted)
        : UiPalette.Foreground;
    }
  }

  private string SelectionLabel() =>
    _selection.Multi
      ? L(_selection.Count + "개 선택됨", _selection.Count + " selected")
      : _kinds.Label(_session.Frames[_selection.Selected].Target);
}
