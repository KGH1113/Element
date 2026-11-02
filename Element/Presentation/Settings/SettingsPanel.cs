using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Settings;

internal sealed partial class SettingsPanel : IDisposable
{
  private readonly ElementSettings _settings;
  private readonly Action _apply;
  private readonly Func<bool> _save;
  private readonly Action _editLayout;
  private readonly Action<bool> _visibility;
  private readonly SettingsUiFactory _ui;
  private readonly GameObject _root;
  private readonly RectTransform _window;
  private readonly Canvas _canvas;
  private readonly GameObject[] _pages = new GameObject[9];
  private readonly Button[] _homeCards = new Button[4];
  private readonly TextMeshProUGUI _title;
  private readonly TextMeshProUGUI _feedback;
  private GameObject _ownedEventSystem;
  private GameObject _previousSelection;
  private GameObject _lastFocused;
  private int _page;
  private int _restoreSelectionFrame = -1;
  private int _screenWidth;
  private int _screenHeight;
  public bool IsOpen => _root != null && _root.activeSelf;
  private static bool Korean => RDString.language == SystemLanguage.Korean;

  private static string L(string ko, string en) => Korean ? ko : en;

  public void Open()
  {
    if (IsOpen)
      return;
    _restoreSelectionFrame = -1;
    UnityModManagerNet.UnityModManager.UI.Instance?.ToggleWindow(false);
    EnsureEventSystem();
    _previousSelection = EventSystem.current?.currentSelectedGameObject;
    SetVisible(true);
    ClearFeedback();
    _ui.RefreshTheme();
    _galleryUi?.RefreshTheme();
    SelectPage(0);
    UpdateShortcutLabel();
    FitWindow();
    _windowMotion.Show();
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public void Close()
  {
    if (!CommitPopover())
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
    _feedback.color = new Color(1, .66f, .53f);
    _retrySave.gameObject.SetActive(true);
    FitWindow();
  }

  private void SetVisible(bool visible)
  {
    if (!visible)
    {
      HidePopover();
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
      if (EventSystem.current?.currentSelectedGameObject == null)
        EventSystem.current?.SetSelectedGameObject(_previousSelection);
      _restoreSelectionFrame = -1;
    }
    SettingsShortcutField capturing = CapturingShortcut;
    if (capturing != null)
    {
      capturing.Tick();
      return;
    }
    if (!IsTyping() && ShortcutPressed())
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
      if (DismissPopover())
        return;
      if (_page == 0)
        Close();
      else
        Back();
      return;
    }
    if (UnityEngine.Input.GetKeyDown(KeyCode.Tab))
    {
      EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.DeactivateInputField();
      MoveFocus();
    }
    if (_screenWidth != Screen.width || _screenHeight != Screen.height)
      FitWindow();
    if (_page == 6 && Time.unscaledTime >= _nextPlanetPreview)
      RefreshPlanetPreview();
    if (_page == ChatterPage)
      RefreshChatterCount();
    GameObject focused = EventSystem.current?.currentSelectedGameObject;
    if (focused != _lastFocused)
    {
      _lastFocused = focused;
      if (focused != null)
        RevealControl(focused.transform, 8);
    }
    UIInteractionState.RestoreCursor();
  }

  public bool ShortcutPressed() => ShortcutPressed(_settings);

  // Static so the runtime can watch the shortcut before the window is built.
  public static bool ShortcutPressed(ElementSettings settings) =>
    ShortcutKeys.Pressed(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  public static bool IsTyping() =>
    EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused == true;

  private SettingsShortcutField CapturingShortcut =>
    _settingsShortcut?.Capturing == true ? _settingsShortcut
    : _presetShortcut?.Capturing == true ? _presetShortcut
    : null;

  private string AssignSettingsShortcut(KeyCode key, KeyCode modifier)
  {
    EffectPreset owner = _settings.Effects.FindShortcut(key, modifier);
    if (owner != null)
      return ShortcutInUse(owner);
    _settings.SettingsShortcutKey = key;
    _settings.SettingsShortcutModifier = modifier;
    Changed();
    return null;
  }

  private void CancelCapture()
  {
    _settingsShortcut?.Cancel();
    _presetShortcut?.Cancel();
  }

  private void UpdateShortcutLabel()
  {
    if (_settingsShortcut == null)
      return;
    _settingsShortcut.Refresh();
    LayoutShortcutControls();
  }

  public static string ShortcutLabel(ElementSettings settings) =>
    ShortcutKeys.Label(settings.SettingsShortcutKey, settings.SettingsShortcutModifier);

  private static bool IsMac => ShortcutKeys.IsMac;

  private void Changed()
  {
    _apply();
    _ui.RefreshTheme();
    _galleryUi?.RefreshTheme();
  }

  private void SelectPage(int index)
  {
    HidePopover();
    CancelCapture();
    if (_scroll.content != null)
      _scrollPositions[_page] = _scroll.verticalNormalizedPosition;
    _page = index;
    for (int i = 0; i < _pages.Length; i++)
      _pages[i].SetActive(i == index);
    _scroll.content = (RectTransform)_pages[index].transform;
    _title.text =
      index == 0 ? L("설정", "Settings")
      : index == 1 ? "Tweaks"
      : index == 2 ? L("일반", "General")
      : index == 3 ? "Effects"
      : index == 4 ? L("에디터", "Editor tweaks")
      : index == 5 ? L("판정 표시", "Judgment visuals")
      : index == 6 ? L("행성", "Planets")
      : index == ChatterPage ? L("채터 블로커", "Chatter blocker")
      : _editingPreset == null ? L("새 프리셋", "New preset")
      : L("프리셋 편집", "Edit preset");
    _back.gameObject.SetActive(index != 0);
    FitWindow();
    Canvas.ForceUpdateCanvases();
    _scroll.StopMovement();
    _scroll.verticalNormalizedPosition = _scrollPositions[index];
    _lastFocused = null;
    RefreshPlanetPreview();
    if (index == 2)
      RefreshUpdateRow();
  }

  private void Back()
  {
    if (!CommitPopover())
      return;
    int previous = _page;
    if (previous == 7)
    {
      SelectPage(3);
      return;
    }
    if (previous >= 4)
    {
      SelectPage(1);
      return;
    }
    SelectPage(0);
    int card =
      previous == 2 ? 3
      : previous == 3 ? 2
      : 1;
    EventSystem.current?.SetSelectedGameObject(_homeCards[card].gameObject);
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
    Selectable[] controls = (
      _planetPicker?.Visible == true ? _planetPicker.Root.gameObject : _root
    ).GetComponentsInChildren<Selectable>(false);
    List<Selectable> active = new();
    foreach (Selectable control in controls)
      if (control.IsInteractable())
        active.Add(control);
    if (active.Count == 0)
      return;
    GameObject selected = EventSystem.current?.currentSelectedGameObject;
    int current = active.FindIndex(x => x.gameObject == selected);
    int next =
      (
        current
        + (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift) ? -1 : 1)
        + active.Count
      ) % active.Count;
    EventSystem.current?.SetSelectedGameObject(active[next].gameObject);
    RevealControl(active[next].transform, 8);
  }

  private void RevealControl(Transform control, float margin)
  {
    if (!control.IsChildOf(_pages[_page].transform))
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

  public void Dispose()
  {
    _root.SetActive(false);
    Object.Destroy(_root);
    if (_ownedEventSystem != null)
      Object.Destroy(_ownedEventSystem);
    _ui.Dispose();
    _planetPicker?.Dispose();
    _galleryUi?.Dispose();
  }
}
