using System.Collections.Generic;
using ImplResourcePack.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ImplResourcePack.Presentation.Settings.SettingsLocale;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor
{
  private GameObject _galleryRoot;
  private bool _resetConfirmation;
  private RectTransform _galleryPanel;
  private SettingsUiFactory _galleryUi;
  private OverlayColorPopover _colorPopover;
  private readonly List<IOverlayLayoutTarget> _galleryTemplates = new();
  private TextMeshProUGUI _galleryError;
  private int _galleryWidth,
    _galleryHeight;

  private static string ElementLabel(IOverlayLayoutTarget target) =>
    target.TemplateId switch
    {
      "progress" => L("진행도", "Progress"),
      "accuracy" => L("정확도", "Accuracy"),
      "time" => L("음악·맵 시간", "Music / map time"),
      "fps" => "FPS",
      "tile-bpm" => L("타일 BPM", "Tile BPM"),
      "current-bpm" => L("현재 BPM", "Current BPM"),
      "keys-per-second" => L("초당 입력 수", "Keys per second"),
      "song-title" => L("곡 제목", "Song title"),
      "combo" => L("콤보", "Combo"),
      "judgement" => L("판정", "Judgments"),
      "timing-scale" => L("타이밍 배율", "Timing scale"),
      "native-hand" => L("손 키뷰어", "Hand key viewer"),
      "native-foot" => L("발 키뷰어", "Foot key viewer"),
      _ => target.Label,
    };

  public Rect NativeExclusionBounds
  {
    get
    {
      if (_galleryRoot != null)
        return new Rect(0, 0, ViewWidth, ViewHeight);
      Rect a = ToolbarBounds;
      if (!(_colorPopover?.Visible ?? false))
        return a;
      Rect b = ColorPopoverBounds;
      return Rect.MinMaxRect(
        Mathf.Min(a.xMin, b.xMin),
        Mathf.Min(a.yMin, b.yMin),
        Mathf.Max(a.xMax, b.xMax),
        Mathf.Max(a.yMax, b.yMax)
      );
    }
  }

  private float PopupScale =>
    Mathf.Min(
      UiScale,
      Mathf.Min((ViewWidth - 16) / OverlayColorPopover.Width, (ViewHeight - 16) / OverlayColorPopover.Height)
    );

  private Rect ColorPopoverBounds
  {
    get
    {
      Rect toolbar = ToolbarBounds;
      float w = OverlayColorPopover.Width * PopupScale,
        h = OverlayColorPopover.Height * PopupScale;
      float y = toolbar.y - h - 10;
      if (y < 8)
        y = toolbar.yMax + 10;
      return new Rect(
        Mathf.Clamp(toolbar.xMax - w, 8, Mathf.Max(8, ViewWidth - w - 8)),
        Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
        w,
        h
      );
    }
  }

  private void ToggleColor()
  {
    if (_colorPopover == null)
      _colorPopover = new OverlayColorPopover(_root.transform, _font, _preferences);
    if (_colorPopover.Visible)
      _colorPopover.Hide();
    else
    {
      CloseGallery();
      _colorPopover.Show();
      UpdatePopupPosition();
    }
    FinishDrag();
    _toolbarDragging = false;
  }

  private void UpdatePopupPosition()
  {
    if (_colorPopover?.Visible ?? false)
    {
      Rect bounds = ColorPopoverBounds;
      SettingsUiFactory.Place(
        _colorPopover.Root,
        bounds.x,
        bounds.y,
        OverlayColorPopover.Width,
        OverlayColorPopover.Height
      );
      _colorPopover.Root.localScale = Vector3.one * PopupScale;
    }
    if (_galleryRoot != null && (_galleryWidth != ViewWidth || _galleryHeight != ViewHeight))
    {
      if (_resetConfirmation)
        OpenResetConfirmation();
      else
        OpenGallery();
    }
  }

  private void DeleteSelected()
  {
    if (!HasSelection)
      return;
    IOverlayLayoutTarget target = _frames[_selected].Target;
    (target as IExternalLayoutOutline)?.SetSelected(false);
    target.SetEnabled(false);
    _selected = _dragging = -1;
    _history.Commit(_host.LayoutSettings);
    RefreshLayouts();
    UpdateFrames();
  }

  private void AddElement(IOverlayLayoutTarget template)
  {
    IOverlayLayoutTarget target = _host.AddElement(template.TemplateId);
    if (target == null)
    {
      target = _addExternalElement?.Invoke(template.TemplateId);
      if (target != null)
        _extraTargets.Add(target);
    }
    if (target == null)
    {
      if (_galleryError != null)
        _galleryError.text = L(
          "요소를 추가하지 못했습니다. 갤러리를 다시 열어주세요.",
          "Couldn't add the element. Reopen the gallery and try again."
        );
      return;
    }
    target.SetEnabled(true);
    target.SetPreview(true);
    Canvas.ForceUpdateCanvases();
    Rect bounds = target.Bounds;
    if (bounds.width > 0 && bounds.height > 0)
    {
      bounds.center = new Vector2(ViewWidth * .5f, ViewHeight * .5f);
      target.SetBounds(bounds);
      target.CommitLayout(true, initialize: true);
    }
    AddFrame(target);
    _selected = _frames.Count - 1;
    _history.Commit(_host.LayoutSettings);
    CloseGallery();
    _toolbar.SetAsLastSibling();
    UpdateFrames();
  }

  private void OpenGallery()
  {
    if (_colorPopover != null && !_colorPopover.Commit())
      return;
    _colorPopover?.Hide();
    CloseGallery();
    FinishDrag();
    _toolbarDragging = false;
    _galleryWidth = ViewWidth;
    _galleryHeight = ViewHeight;
    _galleryUi = new SettingsUiFactory(_font) { ReduceMotion = () => _preferences.ReduceUiMotion };
    _galleryRoot = _galleryUi.Rect("Add Element gallery", _root.transform, 0, 0, ViewWidth, ViewHeight).gameObject;
    Button backdrop = _galleryUi.Button(
      "",
      _galleryRoot.transform,
      0,
      0,
      ViewWidth,
      ViewHeight,
      CloseGallery,
      false,
      false
    );
    backdrop.targetGraphic.color = new Color(0, 0, 0, .7f);
    backdrop.transition = Selectable.Transition.None;
    float scale = Mathf.Min(UiScale, Mathf.Min(ViewWidth / 720f, ViewHeight / 460f));
    float w = Mathf.Min(1000, (ViewWidth - 48) / scale);
    float h = Mathf.Min(690, (ViewHeight - 48) / scale);
    Image panel = _galleryUi.Panel(
      "Gallery",
      _galleryRoot.transform,
      (ViewWidth - w * scale) * .5f,
      (ViewHeight - h * scale) * .5f,
      w,
      h,
      SettingsUiFactory.Background
    );
    panel.raycastTarget = true;
    _galleryPanel = panel.rectTransform;
    _galleryPanel.localScale = Vector3.one * scale;
    _galleryUi.Text(L("요소 추가", "Add Element"), _galleryPanel, 28, 20, w - 160, 40, 26);
    Button close = _galleryUi.IconButton("Close gallery", _galleryPanel, w - 68, 20, 40, CloseGallery, 5);
    close.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    _galleryError = _galleryUi.Text("", _galleryPanel, 28, 62, w - 56, 24, 13, new Color(1, .66f, .53f));
    RectTransform viewport = _galleryUi.Rect("Gallery viewport", _galleryPanel, 20, 90, w - 40, h - 110);
    viewport.gameObject.AddComponent<RectMask2D>();
    Image viewportHit = viewport.gameObject.AddComponent<Image>();
    viewportHit.color = Color.clear;
    ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
    scroll.horizontal = false;
    scroll.movementType = ScrollRect.MovementType.Clamped;
    scroll.scrollSensitivity = 38;
    scroll.inertia = !_preferences.ReduceUiMotion;
    scroll.viewport = viewport;
    _galleryTemplates.Clear();
    foreach (IOverlayLayoutTarget template in _host.GalleryTargets)
      _galleryTemplates.Add(template);
    if (_galleryExtras != null)
      foreach (IOverlayLayoutTarget template in _galleryExtras)
        _galleryTemplates.Add(template);
    const int columns = 3;
    const float gap = 12,
      tileHeight = 172;
    float tileWidth = (w - 56 - gap * (columns - 1)) / columns;
    int rows = Mathf.CeilToInt(_galleryTemplates.Count / (float)columns);
    RectTransform content = _galleryUi.Rect("Elements", viewport, 8, 0, w - 56, rows * (tileHeight + gap));
    scroll.content = content;
    Image scrollTrack = _galleryUi.Panel("Gallery scroll", _galleryPanel, w - 20, 90, 12, h - 110, Color.clear, false);
    scrollTrack.raycastTarget = true;
    Scrollbar scrollbar = scrollTrack.gameObject.AddComponent<Scrollbar>();
    scrollbar.direction = Scrollbar.Direction.BottomToTop;
    scrollbar.transition = Selectable.Transition.None;
    RectTransform slideArea = _galleryUi.Rect("Slide area", scrollTrack.transform, 4, 0, 4, h - 110);
    Image thumb = _galleryUi.Panel("Scroll thumb", slideArea, 0, 0, 4, 40, new Color(.32f, .32f, .32f));
    thumb.rectTransform.sizeDelta = Vector2.zero;
    thumb.rectTransform.anchoredPosition = Vector2.zero;
    scrollbar.handleRect = thumb.rectTransform;
    scrollbar.targetGraphic = thumb;
    scroll.verticalScrollbar = scrollbar;
    scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    for (int i = 0; i < _galleryTemplates.Count; i++)
    {
      IOverlayLayoutTarget template = _galleryTemplates[i];
      Button tile = _galleryUi.Button(
        ElementLabel(template),
        content,
        (i % columns) * (tileWidth + gap),
        (i / columns) * (tileHeight + gap),
        tileWidth,
        tileHeight,
        () => AddElement(template)
      );
      tile.name = "Add " + template.TemplateId;
      tile.navigation = new Navigation { mode = Navigation.Mode.Automatic };
      TextMeshProUGUI label = tile.GetComponentInChildren<TextMeshProUGUI>();
      SettingsUiFactory.Place(label.rectTransform, 16, tileHeight - 42, tileWidth - 48, 30);
      label.alignment = TextAlignmentOptions.MidlineLeft;
      label.fontSize = 16;
      LayoutActionIcon plus = _galleryUi
        .Rect("Add", tile.transform, tileWidth - 34, tileHeight - 36, 18, 18)
        .gameObject.AddComponent<LayoutActionIcon>();
      plus.Kind = LayoutAction.Plus;
      plus.color = SettingsPalette.SoftAccent;
      plus.raycastTarget = false;
      RectTransform previewArea = _galleryUi.Rect("Preview", tile.transform, 16, 16, tileWidth - 32, tileHeight - 68);
      previewArea.gameObject.AddComponent<RectMask2D>();
      if (template is IOverlayGalleryPreview provider)
      {
        GameObject sample = provider.CreateGalleryPreview(previewArea);
        if (sample != null && sample.transform is RectTransform rect)
        {
          Vector2 intrinsic = rect.rect.size;
          float fit = Mathf.Min(
            (tileWidth - 48) / Mathf.Max(1, intrinsic.x),
            (tileHeight - 84) / Mathf.Max(1, intrinsic.y)
          );
          rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
          rect.anchoredPosition = Vector2.zero;
          rect.localScale = Vector3.one * Mathf.Min(1.3f, fit);
        }
      }
    }
    Canvas.ForceUpdateCanvases();
    scroll.verticalNormalizedPosition = 1;
    EventSystem.current?.SetSelectedGameObject(close.gameObject);
    UpdateFrames();
  }

  private void CloseGallery()
  {
    if (_galleryRoot == null)
      return;
    _galleryRoot.SetActive(false);
    SettingsUiFactory.Destroy(_galleryRoot);
    _galleryRoot = null;
    _resetConfirmation = false;
    _galleryUi.Dispose();
    _galleryUi = null;
    _galleryTemplates.Clear();
    if (IsOpen && _add != null)
      EventSystem.current?.SetSelectedGameObject(_add.gameObject);
  }
}
