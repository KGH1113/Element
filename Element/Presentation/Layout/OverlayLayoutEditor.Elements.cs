using System;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Popovers above the toolbar, and adding and deleting elements.
internal sealed partial class OverlayLayoutEditor
{
  private const float AddMenuWidth = 360,
    AddMenuRow = 68;
  private GameObject _modalRoot;
  private SettingsUiFactory _modalUi;
  private OverlayColorPopover _colorPopover;
  private RectTransform _addMenu;
  private SettingsUiFactory _addMenuUi;
  private int _modalWidth,
    _modalHeight;
  private Action _rebuildModal;

  private string ElementLabel(IOverlayLayoutTarget target) =>
    target switch
    {
      TextOverlayElement => L("텍스트", "Text"),
      ShapeOverlayElement shape => shape.Label,
      _ => target.TemplateId switch
      {
        OverlayElementKinds.HandKeyViewer => L("손 키뷰어", "Hand key viewer"),
        OverlayElementKinds.FootKeyViewer => L("발 키뷰어", "Foot key viewer"),
        _ => target.Label,
      },
    };

  /// <summary>Screen pixels native key viewers must leave uncovered for the editor's own UI.</summary>
  public Rect NativeExclusionBounds
  {
    get
    {
      if (_modalRoot != null || (_textEditor != null && _textEditor.IsOpen))
        return new Rect(0, 0, ViewWidth, ViewHeight);
      Rect bounds = ToolbarBounds;
      foreach (Rect popover in VisiblePopoverBounds())
        bounds = Rect.MinMaxRect(
          Mathf.Min(bounds.xMin, popover.xMin),
          Mathf.Min(bounds.yMin, popover.yMin),
          Mathf.Max(bounds.xMax, popover.xMax),
          Mathf.Max(bounds.yMax, popover.yMax)
        );
      return bounds;
    }
  }

  private System.Collections.Generic.IEnumerable<Rect> VisiblePopoverBounds()
  {
    foreach (RectTransform panel in VisiblePanels())
      yield return ScreenRect(panel);
  }

  private System.Collections.Generic.IEnumerable<RectTransform> VisiblePanels()
  {
    if (_colorPopover?.Visible ?? false)
      yield return _colorPopover.Root;
    if (_addMenu != null)
      yield return _addMenu;
    if (_settingsPanel != null && _settingsPanel.gameObject.activeSelf)
      yield return _settingsPanel;
    if (_elementPicker?.Visible ?? false)
      yield return _elementPicker.Root;
    if (_valuePopover?.Visible ?? false)
      yield return _valuePopover.Root;
  }

  private readonly System.Collections.Generic.List<LayoutExclusion> _exclusions = new();

  /// <summary>
  /// Every editor panel with its rounded corners, in screen pixels from the
  /// top left. Native key viewers leave exactly these shapes uncovered.
  /// </summary>
  public System.Collections.Generic.IReadOnlyList<LayoutExclusion> NativeExclusions
  {
    get
    {
      _exclusions.Clear();
      if (_modalRoot != null || (_textEditor != null && _textEditor.IsOpen))
      {
        _exclusions.Add(new LayoutExclusion(new LayoutBounds(0, 0, ViewWidth, ViewHeight), 0));
        return _exclusions;
      }
      AddExclusion(ToolbarBounds, ToolbarScale);
      foreach (RectTransform panel in VisiblePanels())
        AddExclusion(ScreenRect(panel), panel.localScale.x);
      LayoutExclusions.Merge(_exclusions);
      return _exclusions;
    }
  }

  // Panels use the shared 8-unit rounded corner, at their own scale.
  private void AddExclusion(Rect bounds, float scale) =>
    _exclusions.Add(new LayoutExclusion(new LayoutBounds(bounds.x, bounds.y, bounds.width, bounds.height), 8 * scale));

  // Popovers that close on an outside click. The selection's settings popup
  // stays open and never blocks the canvas.
  private bool AnyPopoverVisible =>
    (_colorPopover?.Visible ?? false)
    || _addMenu != null
    || (_elementPicker?.Visible ?? false)
    || (_valuePopover?.Visible ?? false);

  private bool PopoverTyping =>
    (_colorPopover?.IsTyping ?? false)
    || (_elementPicker?.IsTyping ?? false)
    || (_valuePopover?.IsTyping ?? false)
    || (_strokeInput != null && _strokeInput.isFocused)
    || (_textSizeInput != null && _textSizeInput.isFocused);

  private bool PopoverContains(Vector2 point)
  {
    foreach (Rect bounds in VisiblePopoverBounds())
      if (bounds.Contains(point))
        return true;
    return false;
  }

  private void CloseTopPopover()
  {
    if (_valuePopover?.Visible ?? false)
      _valuePopover.Hide();
    else if (_elementPicker?.Visible ?? false)
      _elementPicker.Hide();
    else if (_addMenu != null)
      CloseAddMenu();
    else
      _colorPopover?.Hide();
  }

  private void ClosePopovers()
  {
    _valuePopover?.Hide();
    if (_elementPicker?.Visible ?? false)
      _elementPicker.Hide();
    CloseAddMenu();
    _colorPopover?.Hide();
  }

  /// <summary>Applies typed popover values; false keeps the editor open to fix them.</summary>
  private bool CommitPopovers()
  {
    if (_colorPopover != null && !_colorPopover.Commit())
      return false;
    if (_elementPicker != null && !_elementPicker.Commit())
      return false;
    return _valuePopover == null || !_valuePopover.Visible || _valuePopover.Commit();
  }

  private void RaisePopovers()
  {
    if (_colorPopover?.Visible ?? false)
      _colorPopover.Root.SetAsLastSibling();
    _addMenu?.SetAsLastSibling();
    _settingsPanel?.SetAsLastSibling();
    if (_elementPicker?.Visible ?? false)
      _elementPicker.Root.SetAsLastSibling();
    if (_valuePopover?.Visible ?? false)
      _valuePopover.Root.SetAsLastSibling();
    if (_modalRoot != null)
      _modalRoot.transform.SetAsLastSibling();
    if (_textEditor != null && _textEditor.IsOpen)
      _textEditor.Raise();
  }

  private float PopupScale(float width, float height) =>
    Mathf.Min(UiScale, Mathf.Min((ViewWidth - 16) / width, (ViewHeight - 16) / height));

  // A popover above the toolbar (below it when there is no room), clamped to the view.
  private Rect PopoverAboveToolbar(float width, float height, float toolbarX, bool alignRight)
  {
    Rect toolbar = ToolbarBounds;
    float scale = PopupScale(width, height);
    float w = width * scale,
      h = height * scale;
    float y = toolbar.y - h - 10;
    if (y < 8)
      y = toolbar.yMax + 10;
    float x = alignRight ? toolbar.xMax - w : toolbar.x + toolbarX * ToolbarScale;
    return new Rect(
      Mathf.Clamp(x, 8, Mathf.Max(8, ViewWidth - w - 8)),
      Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
      w,
      h
    );
  }

  private void PlacePopover(RectTransform popover, Rect bounds, float width, float height)
  {
    SettingsUiFactory.Place(popover, bounds.x, bounds.y, width, height);
    popover.localScale = Vector3.one * (bounds.width / width);
  }

  private Rect ScreenRect(RectTransform rect)
  {
    Vector2 position = rect.anchoredPosition;
    Vector2 size = rect.rect.size * rect.localScale.x;
    return new Rect(position.x, -position.y, size.x, size.y);
  }

  private void UpdatePopupPositions()
  {
    if (_colorPopover?.Visible ?? false)
      PlacePopover(
        _colorPopover.Root,
        PopoverAboveToolbar(OverlayColorPopover.Width, OverlayColorPopover.Height, 0, true),
        OverlayColorPopover.Width,
        OverlayColorPopover.Height
      );
    if (_addMenu != null)
    {
      float height = AddMenuHeight;
      PlacePopover(_addMenu, PopoverAboveToolbar(AddMenuWidth, height, 348, false), AddMenuWidth, height);
    }
    UpdateSettingsPosition();
    if (_modalRoot != null && (_modalWidth != ViewWidth || _modalHeight != ViewHeight))
      _rebuildModal?.Invoke();
  }

  private void ToggleColor()
  {
    if (_colorPopover == null)
      _colorPopover = new OverlayColorPopover(_root.transform, _font, _preferences);
    if (_colorPopover.Visible)
      _colorPopover.Hide();
    else
    {
      if (!CommitPopovers())
        return;
      ClosePopovers();
      _colorPopover.Show();
      UpdatePopupPositions();
    }
    FinishDrag();
    _toolbarDragging = false;
  }

  private void DeleteSelected()
  {
    if (!HasSelection)
      return;
    ClosePopovers();
    IOverlayLayoutTarget target = _frames[_selected].Target;
    (target as IExternalLayoutOutline)?.SetSelected(false);
    target.SetEnabled(false);
    _selected = _dragging = -1;
    _history.Commit(_host.LayoutSettings);
    RefreshLayouts();
    UpdateFrames();
  }

  private float AddMenuHeight => 16 + AddMenuRow * 3;

  private void ToggleAddMenu()
  {
    if (_addMenu != null)
    {
      CloseAddMenu();
      return;
    }
    if (!CommitPopovers())
      return;
    ClosePopovers();
    FinishDrag();
    _toolbarDragging = false;
    _addMenuUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    Image panel = _addMenuUi.Panel(
      "Add element menu",
      _root.transform,
      0,
      0,
      AddMenuWidth,
      AddMenuHeight,
      new Color(.105f, .105f, .105f)
    );
    panel.raycastTarget = true;
    _addMenu = panel.rectTransform;
    KeyViewerAvailability viewers = _keyViewers?.Availability ?? KeyViewerAvailability.Unsupported;
    Button text = AddMenuRowButton(
      0,
      LayoutAction.Text,
      L("텍스트", "Text"),
      _host.CanShowText
        ? L("글자에 FPS·정확도 같은 값을 넣어요", "Words with values like FPS or accuracy")
        : L("오버레이 글꼴을 불러오지 못해 쓸 수 없어요", "Unavailable: the overlay font didn't load"),
      _host.CanShowText,
      AddText
    );
    AddMenuRowButton(
      1,
      LayoutAction.KeyViewer,
      L("키뷰어", "Key viewer"),
      viewers switch
      {
        KeyViewerAvailability.Ready => L(
          "ElementNote의 키뷰어를 게임 화면에 놓아요",
          "Places ElementNote's key viewer"
        ),
        KeyViewerAvailability.AppClosed => L(
          "ElementNote를 실행하면 추가할 수 있어요",
          "Open ElementNote to add a key viewer"
        ),
        KeyViewerAvailability.HiddenInApp => L(
          "ElementNote에서 '얼불춤 안에 표시'를 켜 주세요",
          "Turn on 'Show inside ADOFAI' in ElementNote"
        ),
        _ => L("이 운영체제에서는 게임 안 키뷰어를 쓸 수 없어요", "In-game key viewers aren't available here"),
      },
      viewers == KeyViewerAvailability.Ready,
      AddKeyViewer
    );
    AddMenuRowButton(
      2,
      LayoutAction.Shape,
      L("도형", "Shape"),
      L("사각형·삼각형·원으로 글자 뒤 배경을 깔아요", "A square, triangle or circle behind text"),
      true,
      AddShape
    );
    UpdatePopupPositions();
    EventSystem.current?.SetSelectedGameObject(text.gameObject);
  }

  private Button AddMenuRowButton(
    int row,
    LayoutAction icon,
    string title,
    string description,
    bool enabled,
    Action add
  )
  {
    Button button = _addMenuUi.Button("", _addMenu, 8, 8 + row * AddMenuRow, AddMenuWidth - 16, AddMenuRow - 4, add);
    button.name = "Add " + icon;
    button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    button.interactable = enabled;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon glyph = _addMenuUi
      .Rect("Icon", button.transform, 14, 18, 28, 28)
      .gameObject.AddComponent<LayoutActionIcon>();
    glyph.Kind = icon;
    glyph.color = enabled ? SettingsPalette.SoftAccent : SettingsUiFactory.Muted;
    glyph.raycastTarget = false;
    _addMenuUi.Text(title, button.transform, 56, 10, AddMenuWidth - 88, 24, 16);
    TextMeshProUGUI detail = _addMenuUi.Text(
      description,
      button.transform,
      56,
      34,
      AddMenuWidth - 88,
      20,
      12,
      enabled ? SettingsUiFactory.Muted : new Color(1, .66f, .53f)
    );
    detail.textWrappingMode = TextWrappingModes.NoWrap;
    return button;
  }

  private void CloseAddMenu()
  {
    if (_addMenu == null)
      return;
    _addMenu.gameObject.SetActive(false);
    SettingsUiFactory.Destroy(_addMenu.gameObject);
    _addMenu = null;
    _addMenuUi?.Dispose();
    _addMenuUi = null;
    if (IsOpen && _add != null)
      EventSystem.current?.SetSelectedGameObject(_add.gameObject);
  }

  private void AddText()
  {
    CloseAddMenu();
    TextOverlayElement text = _host.AddText(null);
    if (text == null)
      return;
    text.SetPreview(true);
    AddFrame(text);
    _selected = _frames.Count - 1;
    // History records the text once it has content; cancelling removes it.
    OpenTextEditor(text, true);
  }

  private void AddShape()
  {
    CloseAddMenu();
    ShapeOverlayElement shape = _host.AddShape();
    shape.SetPreview(true);
    PlaceNewElement(shape);
  }

  private void AddKeyViewer()
  {
    CloseAddMenu();
    IOverlayLayoutTarget viewer = _keyViewers?.Add(OverlayElementKinds.HandKeyViewer);
    if (viewer == null)
    {
      _keyboardHelp.text = L(
        "키뷰어를 추가하지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
        "Couldn't add the key viewer. Check that ElementNote is open."
      );
      _scaleErrorUntil = Time.unscaledTime + 5;
      return;
    }
    viewer.SetEnabled(true);
    viewer.SetPreview(true);
    PlaceNewElement(viewer);
  }

  // A new element starts in the middle of the view, selected, as one undo step.
  private void PlaceNewElement(IOverlayLayoutTarget target)
  {
    Canvas.ForceUpdateCanvases();
    Rect bounds = target.Bounds;
    if (bounds.width > 0 && bounds.height > 0)
    {
      bounds.center = new Vector2(ViewWidth * .5f, ViewHeight * .5f);
      target.SetBounds(bounds);
      target.CommitLayout(true, initialize: true);
    }
    if (IndexOf(target) < 0)
      AddFrame(target);
    _selected = IndexOf(target);
    _history.Commit(_host.LayoutSettings);
    _toolbar.SetAsLastSibling();
    UpdateFrames();
  }

  // A full-screen confirmation. Rebuilt when the view size settles.
  private RectTransform OpenModal(string name, float width, float height, Action rebuild)
  {
    CloseModal();
    _rebuildModal = rebuild;
    _modalWidth = ViewWidth;
    _modalHeight = ViewHeight;
    _modalUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    _modalRoot = _modalUi.Rect(name, _root.transform, 0, 0, ViewWidth, ViewHeight).gameObject;
    Image backdrop = _modalUi.Panel(
      "Backdrop",
      _modalRoot.transform,
      0,
      0,
      ViewWidth,
      ViewHeight,
      new Color(0, 0, 0, .7f),
      false
    );
    backdrop.raycastTarget = true;
    float scale = Mathf.Min(UiScale, Mathf.Min((ViewWidth - 32) / width, (ViewHeight - 32) / height));
    RectTransform panel = _modalUi
      .Panel(
        "Dialog",
        _modalRoot.transform,
        (ViewWidth - width * scale) * .5f,
        (ViewHeight - height * scale) * .5f,
        width,
        height,
        SettingsUiFactory.Background
      )
      .rectTransform;
    panel.localScale = Vector3.one * scale;
    return panel;
  }

  private void CloseModal()
  {
    if (_modalRoot == null)
      return;
    _modalRoot.SetActive(false);
    SettingsUiFactory.Destroy(_modalRoot);
    _modalRoot = null;
    _rebuildModal = null;
    _modalUi?.Dispose();
    _modalUi = null;
  }
}
