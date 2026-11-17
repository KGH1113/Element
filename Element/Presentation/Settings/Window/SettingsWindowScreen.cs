using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Motion = ElementUI.Motion;
using Object = UnityEngine.Object;

namespace Element.Presentation.Settings.Window;

/// <summary>
/// The settings window's place in the game: its canvas, its size on screen,
/// opening and closing, Escape, Tab and the shortcut. What the window shows
/// is an ElementUI screen (<see cref="SettingsWindowView"/>).
/// </summary>
internal sealed class SettingsWindowScreen : ISettingsWindowActions, IDisposable
{
  private const float DesignWidth = 1000,
    DesignHeight = 600;

  private readonly ElementSettings _settings;
  private readonly Func<bool> _save;
  private readonly Action _editLayout;
  private readonly Action<bool> _visibility;
  private readonly TMP_FontAsset _font;
  private readonly SettingsFeatures _features;
  private readonly Action _apply;
  private readonly GameObject _root;
  private readonly Canvas _canvas;
  private readonly RectTransform _window;
  private readonly State<bool> _appeared = new(false);
  private SettingsWindowViewModel _model;
  private UiHost _host;
  private GameObject _ownedEventSystem;
  private GameObject _previousSelection;
  private int _restoreSelectionFrame = -1;
  private int _screenWidth;
  private int _screenHeight;
  private int _highlightVersion = -1;

  /// <param name="apply">Applies the settings after a change.</param>
  /// <param name="save">Writes the settings file; false when it couldn't.</param>
  /// <param name="editLayout">Opens the overlay editor.</param>
  /// <param name="visibility">The window was shown or hidden.</param>
  public SettingsWindowScreen(
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
    _features = features;
    _root = new GameObject("Element Settings", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    _canvas = _root.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _canvas.sortingOrder = 32760;
    _root.AddComponent<GraphicRaycaster>();
    Image scrim = new GameObject("Backdrop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
    scrim.transform.SetParent(_root.transform, false);
    scrim.color = new Color(0, 0, 0, .42f);
    RectPeer.Stretch(scrim.rectTransform, 0, 0, 0, 0);
    _window = (RectTransform)new GameObject("Settings window", typeof(RectTransform)).transform;
    _window.SetParent(_root.transform, false);
    _window.anchorMin = _window.anchorMax = _window.pivot = new Vector2(.5f, .5f);
    _window.sizeDelta = new Vector2(DesignWidth, DesignHeight);
    Mount();
    _root.SetActive(false);
  }

  /// <summary>Builds the window's screen: on first use, and again when the highlight color or motion setting changed.</summary>
  private void Mount()
  {
    _model?.Dispose();
    _model = new SettingsWindowViewModel(_settings, _features, this);
    ElementBackend backend = new(new ElementTheme(UiFonts.Choose(_font), _settings.ReduceUiMotion));
    _host = UiHost.Mount(
      _window,
      backend,
      new SettingsWindowView(_model)
        .Background(UiPalette.Background, 16, new Color(.22f, .22f, .22f))
        .Opacity(() => _appeared.Value ? 1f : 0f)
        .Animation(Motion.ExponentialOut(.24f, 24), () => _appeared.Value)
        .Offset(y: () => _appeared.Value ? 0f : 8f)
        .Animation(Motion.CriticallyDamped(.24f, 32), () => _appeared.Value)
    );
    _host.Root.Edited += _model.Changed;
    _highlightVersion = UiTheme.Version;
  }

  public bool IsOpen => _root != null && _root.activeSelf;

  public void Open()
  {
    if (IsOpen)
      return;
    _restoreSelectionFrame = -1;
    UnityModManagerNet.UnityModManager.UI.Instance?.ToggleWindow(false);
    EnsureEventSystem();
    _previousSelection = Selected();
    if (_highlightVersion != UiTheme.Version)
      Mount();
    SyncMotion();
    SetVisible(true);
    _model.Opened();
    FitWindow();
    UI.WithAnimation(_settings.ReduceUiMotion ? null : Motion.CriticallyDamped(.24f, 32), () => _appeared.Value = true);
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public void Close()
  {
    if (!IsOpen || !_model.Save())
      return;
    CancelCapture();
    SetVisible(false);
    foreach (SettingsPageViewModel page in _model.Pages)
      page.Closed();
    _restoreSelectionFrame = Time.frameCount + 1;
  }

  public void HideForLayout()
  {
    CancelCapture();
    SetVisible(false);
  }

  /// <summary>Shows a preset applied from outside the window, such as by its shortcut.</summary>
  public void RefreshPresets() => ElementUI.Runtime.Probe();

  public void ShowSaveError() => _model.Save();

  // ---- What the window asks for ----

  void ISettingsWindowActions.Apply() => Changed();

  bool ISettingsWindowActions.Save() => _save();

  void ISettingsWindowActions.EditLayout() => EditLayout();

  void ISettingsWindowActions.Close() => Close();

  private void EditLayout()
  {
    HideForLayout();
    _editLayout();
  }

  private void Changed()
  {
    _apply();
    // The highlight color is drawn into the window; a new one needs the window
    // built again, on the page the player was on.
    if (_highlightVersion != UiTheme.Version)
    {
      int page = System.Array.IndexOf(_model.Pages, _model.Current);
      Mount();
      _model.Opened();
      if (page > 0 && page < _model.Pages.Length)
        _model.Show(_model.Pages[page]);
      ElementUI.Runtime.Probe();
    }
    SyncMotion();
  }

  private void SyncMotion()
  {
    if (_host?.Root?.Backend is ElementBackend backend)
      backend.ElementTheme.SetReduceMotion(_settings.ReduceUiMotion);
  }

  private void SetVisible(bool visible)
  {
    if (!visible)
    {
      EventSystem.current?.SetSelectedGameObject(null);
      UI.WithAnimation(null, () => _appeared.Value = false);
      ElementUI.Runtime.Flush();
    }
    _root.SetActive(visible);
    _visibility(visible);
    if (!visible && _ownedEventSystem != null)
    {
      _ownedEventSystem.SetActive(false);
      RectPeer.DestroyObject(_ownedEventSystem);
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
    foreach (SettingsPageViewModel page in _model.Pages)
      if (page.TickCapture())
        return;
    if (!Keyboard.IsTaken && ShortcutPressed(_settings))
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
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      if (_host.HandleEscape() || _model.Current.HandleEscape())
        return;
      if (_model.Current.Parent == null)
        Close();
      else
        _model.Back.TryExecute();
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
    _model.Current.Tick();
    UiInputCapture.RestoreCursor();
  }

  // Static so the runtime can watch the shortcut before the window is built.
  public static bool ShortcutPressed(ElementSettings settings) =>
    ShortcutKeys.Pressed(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  public static string ShortcutLabel(ElementSettings settings) =>
    ShortcutKeys.Label(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  private static GameObject Selected() => Selected(EventSystem.current);

  /// <summary>The selected object, or null; a selection destroyed with its scene reads as none.</summary>
  private static GameObject Selected(EventSystem events)
  {
    if (events == null)
      return null;
    GameObject selected = events.currentSelectedGameObject;
    return selected != null ? selected : null;
  }

  private void CancelCapture()
  {
    foreach (SettingsPageViewModel page in _model.Pages)
      page.CancelCapture();
  }

  private void EnsureEventSystem()
  {
    if (EventSystem.current != null && EventSystem.current.gameObject != _ownedEventSystem)
    {
      if (_ownedEventSystem != null)
        RectPeer.DestroyObject(_ownedEventSystem);
      _ownedEventSystem = null;
      return;
    }
    if (EventSystem.current != null)
      return;
    _ownedEventSystem = new GameObject("Element UI Events", typeof(EventSystem), typeof(StandaloneInputModule));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_ownedEventSystem);
  }

  private void MoveFocus()
  {
    Selectable[] controls = _window.GetComponentsInChildren<Selectable>(false);
    List<Selectable> active = new();
    foreach (Selectable control in controls)
      if (control.IsInteractable())
        active.Add(control);
    if (active.Count == 0)
      return;
    GameObject selected = EventSystem.current?.currentSelectedGameObject;
    int current = active.FindIndex(x => x.gameObject == selected);
    int next = (current + (UiModifierKeys.Shift ? -1 : 1) + active.Count) % active.Count;
    EventSystem.current?.SetSelectedGameObject(active[next].gameObject);
  }

  private void FitWindow() => Resize(Screen.width, Screen.height);

  /// <summary>Sizes the window for a screen of <paramref name="pixelWidth"/> × <paramref name="pixelHeight"/>.</summary>
  internal void Resize(int pixelWidth, int pixelHeight)
  {
    _screenWidth = pixelWidth;
    _screenHeight = pixelHeight;
    float scale = Mathf.Clamp(pixelHeight / 1000f, 1f, 2f);
    _canvas.scaleFactor = scale;
    float width = Mathf.Max(280, Mathf.Min(DesignWidth, pixelWidth / scale - 32));
    float height = Mathf.Max(240, Mathf.Min(DesignHeight, pixelHeight / scale - 32));
    _window.sizeDelta = new Vector2(width, height);
    _model?.Resized(width);
    // Unity tells the host about the new size only in play mode; say it directly.
    _host?.Root.Resize(new ElementUI.Size(width, height));
  }

  public void Dispose()
  {
    _model?.Dispose();
    _root.SetActive(false);
    RectPeer.DestroyObject(_root);
    if (_ownedEventSystem != null)
      RectPeer.DestroyObject(_ownedEventSystem);
  }
}
