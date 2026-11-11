using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Settings.Pages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;
using Object = UnityEngine.Object;

namespace Element.Presentation.Settings;

/// <summary>
/// The settings window: its frame, moving between pages, Back and Escape,
/// keyboard focus and saving. Each page builds and lays out its own controls.
/// </summary>
internal sealed class SettingsPanel : ISettingsWindow, IDisposable
{
  private readonly ElementSettings _settings;
  private readonly Action _apply;
  private readonly Func<bool> _save;
  private readonly Action _editLayout;
  private readonly Action<bool> _visibility;
  private readonly TMP_FontAsset _font;
  private readonly SettingsUiFactory _ui;
  private readonly GameObject _root;
  private readonly Canvas _canvas;
  private readonly RectTransform _window;
  private readonly CanvasGroup _windowGroup;
  private readonly SettingsWindowMotion _windowMotion;
  private readonly RectTransform _surface,
    _viewport;
  private readonly ScrollRect _scroll;
  private readonly Scrollbar _scrollbar;
  private readonly Button _close,
    _back,
    _retrySave;
  private readonly TextMeshProUGUI _title,
    _feedback;
  private readonly SettingsPopoverShade _shade;
  private readonly HomePage _home;
  private readonly TweaksPage _tweaks;
  private readonly GeneralPage _general;
  private readonly EffectsPage _effects;
  private readonly EditorTweaksPage _editorTweaks;
  private readonly JudgmentPage _judgment;
  private readonly PlanetsPage _planets;
  private readonly PresetEditorPage _presetEditor;
  private readonly ChatterPage _chatter;
  private readonly ProfilesPage _profiles;

  // Every page, in the order they record keys and are laid out.
  private readonly SettingsPage[] _pages;
  private SettingsPage _current;
  private GameObject _ownedEventSystem;
  private GameObject _previousSelection;
  private GameObject _lastFocused;
  private int _restoreSelectionFrame = -1;
  private int _screenWidth;
  private int _screenHeight;

  // TMP places the caret of a field focused before its text is laid out at
  // nothing, and keeps it there until typing; the next frame places it again.
  private TMP_InputField _focusField;
  private int _focusFrame;

  /// <param name="apply">Applies the settings after a change.</param>
  /// <param name="save">Writes the settings file; false when it couldn't.</param>
  /// <param name="editLayout">Opens the overlay editor.</param>
  /// <param name="visibility">The window was shown or hidden.</param>
  public SettingsPanel(
    ElementSettings settings,
    TMP_FontAsset font,
    Action apply,
    Func<bool> save,
    Action editLayout,
    Action<bool> visibility,
    SettingsFeatures features
  )
  {
    _settings = settings;
    settings.Gameplay.Normalize();
    settings.Effects.Normalize();
    _apply = apply;
    _save = save;
    _editLayout = editLayout;
    _visibility = visibility;
    _font = font;
    _ui = new SettingsUiFactory(font) { ReduceMotion = () => settings.ReduceUiMotion };
    _root = new GameObject("Element Settings", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    _canvas = _root.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _canvas.sortingOrder = 32760;
    _root.AddComponent<GraphicRaycaster>();
    Image scrim = _ui.Panel("Backdrop", _root.transform, 0, 0, 0, 0, new Color(0, 0, 0, .42f), false);
    SettingsUiFactory.Stretch(scrim.rectTransform, 0, 0, 0, 0);
    scrim.raycastTarget = true;
    _window = _ui.Panel("Settings window", _root.transform, 0, 0, 1000, 600, new Color(.22f, .22f, .22f)).rectTransform;
    _window.anchorMin = _window.anchorMax = _window.pivot = new Vector2(.5f, .5f);
    _window.anchoredPosition = Vector2.zero;
    _windowGroup = _window.gameObject.AddComponent<CanvasGroup>();
    _windowMotion = _window.gameObject.AddComponent<SettingsWindowMotion>();
    _windowMotion.Initialize(_windowGroup, () => _settings.ReduceUiMotion);
    _surface = _ui.Panel("Surface", _window, 1, 1, 998, 598, SettingsUiFactory.Background).rectTransform;
    _back = _ui.IconButton("Back", _window, 32, 24, 40, Back, SettingsNavigationIconKind.Back);
    _title = _ui.Text(L("설정", "Settings"), _window, 32, 24, 700, 40, 27);
    _close = _ui.IconButton("Close", _window, 928, 24, 40, Close, SettingsNavigationIconKind.Close);
    _viewport = _ui.Panel("Content viewport", _window, 32, 96, 936, 480, Color.clear, false).rectTransform;
    _viewport.GetComponent<Image>().raycastTarget = true;
    _viewport.gameObject.AddComponent<RectMask2D>();
    _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
    _scroll.viewport = _viewport;
    _scroll.horizontal = false;
    _scroll.vertical = true;
    _scroll.movementType = ScrollRect.MovementType.Clamped;
    _scroll.scrollSensitivity = 28;
    _scroll.decelerationRate = .08f;
    Image scrollTrack = _ui.Panel("Scroll", _window, 976, 96, 12, 480, Color.clear, false);
    scrollTrack.raycastTarget = true;
    _scrollbar = scrollTrack.gameObject.AddComponent<Scrollbar>();
    _scrollbar.direction = Scrollbar.Direction.BottomToTop;
    _scrollbar.transition = Selectable.Transition.None;
    RectTransform slideArea = _ui.Rect("Slide area", scrollTrack.transform, 4, 0, 4, 480);
    SettingsUiFactory.Stretch(slideArea, 4, 0, 4, 0);
    Image scrollHandle = _ui.Panel("Scroll thumb", slideArea, 0, 0, 4, 40, new Color(.32f, .32f, .32f));
    scrollHandle.rectTransform.sizeDelta = Vector2.zero;
    scrollHandle.rectTransform.anchoredPosition = Vector2.zero;
    _scrollbar.handleRect = scrollHandle.rectTransform;
    _scrollbar.targetGraphic = scrollHandle;
    _scroll.verticalScrollbar = _scrollbar;
    _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    _feedback = _ui.Text("", _window, 32, 540, 780, 40, 14, SettingsUiFactory.Muted);
    _retrySave = _ui.Button(
      L("다시 저장", "Retry save"),
      _window,
      848,
      540,
      120,
      40,
      () =>
      {
        if (_save())
          ClearFeedback();
      }
    );
    _retrySave.gameObject.SetActive(false);
    _shade = new SettingsPopoverShade(
      _ui,
      _window,
      () =>
      {
        if (CommitPopovers())
          HidePopovers();
      }
    );
    // A page is built before the page that links to it.
    _editorTweaks = new EditorTweaksPage(this);
    _judgment = new JudgmentPage(this, features.JudgementTextLinked);
    _planets = new PlanetsPage(this, features.RenderPlanetPreview);
    _chatter = new ChatterPage(this, features.BlockedChatter);
    _tweaks = new TweaksPage(this, _editorTweaks, _judgment, _planets, _chatter);
    _presetEditor = new PresetEditorPage(this);
    _effects = new EffectsPage(this, _presetEditor);
    if (features.Profiles != null)
      _profiles = new ProfilesPage(this, features.Profiles, features.OverlayFonts);
    _general = new GeneralPage(this, _profiles, features.Updates);
    _home = new HomePage(this, _close, _tweaks, _effects, _general, _profiles != null);
    var pages = new List<SettingsPage>
    {
      _home,
      _tweaks,
      _general,
      _effects,
      _editorTweaks,
      _judgment,
      _planets,
      _presetEditor,
      _chatter,
    };
    if (_profiles != null)
      pages.Add(_profiles);
    _pages = pages.ToArray();
    SelectPage(_home);
    FitWindow();
    _root.SetActive(false);
  }

  public bool IsOpen => _root != null && _root.activeSelf;

  ElementSettings ISettingsWindow.Settings => _settings;
  SettingsUiFactory ISettingsWindow.Ui => _ui;
  TMP_FontAsset ISettingsWindow.Font => _font;
  RectTransform ISettingsWindow.Frame => _window;
  RectTransform ISettingsWindow.Viewport => _viewport;
  SettingsPopoverShade ISettingsWindow.Shade => _shade;

  public void Open()
  {
    if (IsOpen)
      return;
    _restoreSelectionFrame = -1;
    UnityModManagerNet.UnityModManager.UI.Instance?.ToggleWindow(false);
    EnsureEventSystem();
    _previousSelection = Selected();
    SetVisible(true);
    ClearFeedback();
    foreach (SettingsPage page in _pages)
      page.Opened();
    RefreshThemes();
    SelectPage(_home);
    FitWindow();
    _windowMotion.Show();
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public void Close()
  {
    if (!CommitPopovers())
      return;
    if (!IsOpen || !Commit())
      return;
    CancelCapture();
    SetVisible(false);
    _restoreSelectionFrame = Time.frameCount + 1;
  }

  public void HideForLayout()
  {
    CancelCapture();
    SetVisible(false);
  }

  /// <summary>Shows a preset applied from outside the window, such as by its shortcut.</summary>
  public void RefreshPresets() => _effects.RefreshPresetGallery();

  private bool Commit()
  {
    if (_save())
      return true;
    ShowSaveError();
    return false;
  }

  public void ShowSaveError()
  {
    _feedback.text = L(
      "설정을 저장하지 못했어요. 모드 폴더의 쓰기 권한을 확인하고 다시 닫아주세요.",
      "Settings could not be saved. Check write permission for the mod folder, then close again."
    );
    _feedback.color = SettingsPalette.Error;
    _retrySave.gameObject.SetActive(true);
    FitWindow();
  }

  private void ClearFeedback()
  {
    _feedback.text = "";
    _retrySave.gameObject.SetActive(false);
    FitWindow();
  }

  private void SetVisible(bool visible)
  {
    if (!visible)
    {
      HidePopovers();
      EventSystem.current?.SetSelectedGameObject(null);
    }
    _root.SetActive(visible);
    _visibility(visible);
    if (!visible && _ownedEventSystem != null)
    {
      _ownedEventSystem.SetActive(false);
      Object.Destroy(_ownedEventSystem);
      _ownedEventSystem = null;
    }
  }

  public void Tick()
  {
    if (!IsOpen && _restoreSelectionFrame >= 0 && Time.frameCount >= _restoreSelectionFrame)
    {
      // The selection from before opening may have gone with its scene.
      if (EventSystem.current != null && Selected() == null && _previousSelection != null)
        EventSystem.current.SetSelectedGameObject(_previousSelection);
      _restoreSelectionFrame = -1;
    }
    for (int i = 0; i < _pages.Length; i++)
      if (_pages[i].TickCapture())
        return;
    if (!IsTyping() && ShortcutPressed(_settings))
    {
      if (IsOpen)
        Close();
      else
        Open();
      return;
    }
    if (!IsOpen)
      return;
    EnsureEventSystem();
    SettleFocus();
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      if (_current.HandleEscape())
        return;
      if (_current.Parent == null)
        Close();
      else
        Back();
      return;
    }
    if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
    {
      TMP_InputField field = Selected()?.GetComponent<TMP_InputField>();
      if (field != null)
        field.DeactivateInputField();
      MoveFocus();
    }
    if (_screenWidth != Screen.width || _screenHeight != Screen.height)
      FitWindow();
    _current.Tick();
    GameObject focused = Selected();
    if (focused != _lastFocused)
    {
      _lastFocused = focused;
      if (focused != null)
        RevealControl(focused.transform, 8);
    }
    SettingsInputCapture.RestoreCursor();
  }

  // Static so the runtime can watch the shortcut before the window is built.
  public static bool ShortcutPressed(ElementSettings settings) =>
    ShortcutKeys.Pressed(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  public static string ShortcutLabel(ElementSettings settings) =>
    ShortcutKeys.Label(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  public static bool IsTyping() => IsTyping(EventSystem.current);

  internal static bool IsTyping(EventSystem events)
  {
    TMP_InputField field = Selected(events)?.GetComponent<TMP_InputField>();
    return field != null && field.isFocused;
  }

  private static GameObject Selected() => Selected(EventSystem.current);

  /// <summary>
  /// The selected object, or null. A selection destroyed with its scene is
  /// Unity's fake null, which <c>?.</c> lets through; it reads as none here.
  /// </summary>
  private static GameObject Selected(EventSystem events)
  {
    if (events == null)
      return null;
    GameObject selected = events.currentSelectedGameObject;
    return selected != null ? selected : null;
  }

  private void CancelCapture()
  {
    foreach (SettingsPage page in _pages)
      page.CancelCapture();
  }

  private void Changed()
  {
    _apply();
    RefreshThemes();
  }

  private void RefreshThemes()
  {
    _ui.RefreshTheme();
    foreach (SettingsPage page in _pages)
      page.RefreshTheme();
  }

  void ISettingsWindow.SettingsReplaced()
  {
    foreach (SettingsPage page in _pages)
      page.SettingsReplaced();
    RefreshThemes();
  }

  bool ISettingsWindow.IsShowing(SettingsPage page) => IsOpen && _current == page;

  void ISettingsWindow.Changed() => Changed();

  void ISettingsWindow.FitWindow() => FitWindow();

  bool ISettingsWindow.CommitPopovers() => CommitPopovers();

  void ISettingsWindow.HidePopovers() => HidePopovers();

  void ISettingsWindow.ShowPage(SettingsPage page) => SelectPage(page);

  void ISettingsWindow.OpenPage(SettingsPage page)
  {
    SelectPage(page);
    EventSystem.current?.SetSelectedGameObject(_back.gameObject);
  }

  void ISettingsWindow.EditLayout()
  {
    if (!Commit())
      return;
    HideForLayout();
    _editLayout();
  }

  bool ISettingsWindow.Save()
  {
    if (_save())
      return true;
    ShowSaveError();
    return false;
  }

  private bool CommitPopovers()
  {
    foreach (SettingsPage page in _pages)
      if (!page.CommitPopovers())
        return false;
    return true;
  }

  private void HidePopovers()
  {
    foreach (SettingsPage page in _pages)
      page.HidePopovers();
    _shade.Hide();
  }

  void ISettingsWindow.Focus(TMP_InputField field)
  {
    field.Select();
    field.ActivateInputField();
    _focusField = field;
    _focusFrame = Time.frameCount;
  }

  private void SettleFocus()
  {
    if (_focusField is null || Time.frameCount <= _focusFrame)
      return;
    TMP_InputField field = _focusField;
    _focusField = null;
    if (field == null || !field.isFocused)
      return;
    field.textComponent.ForceMeshUpdate();
    field.MoveTextEnd(false);
  }

  private void SelectPage(SettingsPage page)
  {
    HidePopovers();
    CancelCapture();
    if (_current != null && _scroll.content != null)
      _current.ScrollPosition = _scroll.verticalNormalizedPosition;
    _current = page;
    foreach (SettingsPage each in _pages)
      each.Root.gameObject.SetActive(each == page);
    _scroll.content = page.Root;
    _title.text = page.Title;
    _back.gameObject.SetActive(page.Parent != null);
    FitWindow();
    Canvas.ForceUpdateCanvases();
    _scroll.StopMovement();
    _scroll.verticalNormalizedPosition = page.ScrollPosition;
    _lastFocused = null;
    page.Shown();
  }

  private void Back()
  {
    if (!CommitPopovers())
      return;
    SettingsPage previous = _current;
    if (previous.Parent == null)
      return;
    SelectPage(previous.Parent);
    if (previous.ReturnFocus != null)
      EventSystem.current?.SetSelectedGameObject(previous.ReturnFocus.gameObject);
  }

  private void EnsureEventSystem()
  {
    if (EventSystem.current != null && EventSystem.current.gameObject != _ownedEventSystem)
    {
      if (_ownedEventSystem != null)
        Object.Destroy(_ownedEventSystem);
      _ownedEventSystem = null;
      return;
    }
    if (EventSystem.current != null)
      return;
    _ownedEventSystem = new GameObject("Element UI Events", typeof(EventSystem), typeof(StandaloneInputModule));
    Object.DontDestroyOnLoad(_ownedEventSystem);
  }

  private void MoveFocus()
  {
    GameObject scope = _current.FocusScope;
    if (scope == null)
      scope = _root;
    Selectable[] controls = scope.GetComponentsInChildren<Selectable>(false);
    List<Selectable> active = new();
    foreach (Selectable control in controls)
      if (control.IsInteractable())
        active.Add(control);
    if (active.Count == 0)
      return;
    GameObject selected = EventSystem.current?.currentSelectedGameObject;
    int current = active.FindIndex(x => x.gameObject == selected);
    int next = (current + (SettingsModifierKeys.Shift ? -1 : 1) + active.Count) % active.Count;
    EventSystem.current?.SetSelectedGameObject(active[next].gameObject);
    RevealControl(active[next].transform, 8);
  }

  private void RevealControl(Transform control, float margin)
  {
    if (!control.IsChildOf(_current.Root))
      return;
    Canvas.ForceUpdateCanvases();
    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, control);
    Rect view = _viewport.rect;
    float delta =
      bounds.min.y - margin < view.yMin ? view.yMin - bounds.min.y + margin
      : bounds.max.y + margin > view.yMax ? view.yMax - bounds.max.y - margin
      : 0;
    Vector2 position = _scroll.content.anchoredPosition;
    position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, _scroll.content.rect.height - view.height));
    _scroll.StopMovement();
    _scroll.content.anchoredPosition = position;
  }

  private void FitWindow() => Resize(Screen.width, Screen.height);

  internal void Resize(int pixelWidth, int pixelHeight)
  {
    // Nothing to lay out before the first page is shown.
    if (_current == null)
      return;
    _screenWidth = pixelWidth;
    _screenHeight = pixelHeight;
    float scale = Mathf.Clamp(pixelHeight / 1000f, 1f, 2f);
    _canvas.scaleFactor = scale;
    float width = Mathf.Max(280, Mathf.Min(1000, pixelWidth / scale - 32));
    float height = Mathf.Max(240, Mathf.Min(600, pixelHeight / scale - 32));
    _window.sizeDelta = new Vector2(width, height);
    SettingsUiFactory.Place(_surface, 1, 1, width - 2, height - 2);
    float inset = width < 600 ? 20 : 32;
    float contentWidth = width - inset * 2;
    float titleLeft = _current.Parent == null ? inset : inset + 52;
    SettingsUiFactory.Place((RectTransform)_back.transform, inset, 24, 40, 40);
    SettingsUiFactory.Place(_title.rectTransform, titleLeft, 24, width - titleLeft - inset - 48, 40);
    SettingsUiFactory.Place((RectTransform)_close.transform, width - inset - 40, 24, 40, 40);
    float bottom = string.IsNullOrEmpty(_feedback.text) ? 24 : 80;
    float viewHeight = Mathf.Max(80, height - 96 - bottom);
    SettingsUiFactory.Place(_viewport, inset, 96, contentWidth, viewHeight);
    SettingsUiFactory.Place((RectTransform)_scrollbar.transform, width - inset + 6, 96, 12, viewHeight);
    _shade.Layout();
    foreach (SettingsPage page in _pages)
      page.Layout(contentWidth, viewHeight);
    SettingsUiFactory.Place(_feedback.rectTransform, inset, height - 64, contentWidth - 132, 48);
    SettingsUiFactory.Place((RectTransform)_retrySave.transform, width - inset - 120, height - 60, 120, 40);
  }

  public void Dispose()
  {
    _root.SetActive(false);
    Object.Destroy(_root);
    if (_ownedEventSystem != null)
      Object.Destroy(_ownedEventSystem);
    _ui.Dispose();
    foreach (SettingsPage page in _pages)
      page.Dispose();
  }
}
