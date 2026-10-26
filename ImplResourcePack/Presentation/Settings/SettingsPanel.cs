using System;
using System.Collections.Generic;
using ImplResourcePack.Configuration;
using ImplResourcePack.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Settings;

internal sealed partial class SettingsPanel : IDisposable
{
  private readonly ImplResourcePackSettings _settings;
  private readonly Action _apply;
  private readonly Func<bool> _save;
  private readonly Action _editLayout;
  private readonly Action<bool> _visibility;
  private readonly SettingsUiFactory _ui;
  private readonly GameObject _root;
  private readonly RectTransform _window;
  private readonly Canvas _canvas;
  private readonly GameObject[] _pages = new GameObject[8];
  private readonly Button[] _homeCards = new Button[4];
  private readonly TextMeshProUGUI _title;
  private readonly TextMeshProUGUI _feedback;
  private TextMeshProUGUI _shortcutLabel;
  private TextMeshProUGUI _captureHint;
  private GameObject _ownedEventSystem;
  private GameObject _previousSelection;
  private GameObject _lastFocused;
  private bool _capturing;
  private int _page;
  private int _captureStarted;
  private int _restoreSelectionFrame = -1;
  private int _screenWidth;
  private int _screenHeight;
  private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
  private static readonly KeyCode[] Modifiers =
  {
    KeyCode.LeftAlt,
    KeyCode.RightAlt,
    KeyCode.LeftControl,
    KeyCode.RightControl,
    KeyCode.LeftShift,
    KeyCode.RightShift,
    KeyCode.LeftCommand,
    KeyCode.RightCommand,
  };
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
    if (_capturing)
    {
      CaptureShortcut();
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
  public static bool ShortcutPressed(ImplResourcePackSettings settings)
  {
    if (!UnityEngine.Input.GetKeyDown(settings.SettingsShortcutKey))
      return false;
    foreach (KeyCode modifier in Modifiers)
      if (UnityEngine.Input.GetKey(modifier) != (modifier == settings.SettingsShortcutModifier))
        return false;
    return true;
  }

  public static bool IsTyping() =>
    EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused == true;

  private void BeginCapture()
  {
    _capturing = true;
    _keycaps.gameObject.SetActive(false);
    _shortcutLabel.gameObject.SetActive(true);
    EventSystem.current?.SetSelectedGameObject(null);
    _captureStarted = Time.frameCount;
    _shortcutLabel.text = L("새 단축키를 입력하세요…", "Press a shortcut…");
    _captureHint.text = L(
      "보조 키 하나와 일반 키를 함께 누를 수 있어요. Esc로 취소합니다.",
      "Use a key with up to one modifier. Esc cancels."
    );
    FitWindow();
  }

  private void CaptureShortcut()
  {
    if (Time.frameCount == _captureStarted)
      return;
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      CancelCapture();
      return;
    }
    foreach (KeyCode key in Keys)
    {
      if (key == KeyCode.None || key >= KeyCode.Mouse0 || IsModifier(key) || !UnityEngine.Input.GetKeyDown(key))
        continue;
      KeyCode modifier = KeyCode.None;
      int count = 0;
      foreach (KeyCode candidate in Modifiers)
        if (UnityEngine.Input.GetKey(candidate))
        {
          modifier = candidate;
          count++;
        }
      if (count > 1)
      {
        _captureHint.text = L(
          "보조 키는 하나만 선택해주세요. 예: 왼쪽 Alt + Tab",
          "Choose one modifier, for example Left Alt + Tab."
        );
        return;
      }
      _settings.SettingsShortcutKey = key;
      _settings.SettingsShortcutModifier = modifier;
      CancelCapture();
      Changed();
      return;
    }
  }

  private static bool IsModifier(KeyCode key) => Array.IndexOf(Modifiers, key) >= 0;

  private void CancelCapture()
  {
    bool captured = _capturing;
    _capturing = false;
    if (_captureHint != null)
      _captureHint.text = "";
    UpdateShortcutLabel();
    FitWindow();
    if (captured && IsOpen)
      EventSystem.current?.SetSelectedGameObject(_capture.gameObject);
  }

  private void UpdateShortcutLabel()
  {
    if (_shortcutLabel != null)
      _shortcutLabel.text = ShortcutLabel(_settings);
    if (_keycaps == null)
      return;
    _shortcutLabel.gameObject.SetActive(false);
    _keycaps.gameObject.SetActive(true);
    bool modifier = _settings.SettingsShortcutModifier != KeyCode.None;
    _modifierKey.transform.parent.gameObject.SetActive(modifier);
    _keyPlus.gameObject.SetActive(modifier);
    _modifierKey.text = ModifierLabel(_settings.SettingsShortcutModifier);
    bool symbol = modifier && IsMac && SettingsKeySymbol.Symbol(_settings.SettingsShortcutModifier).Length > 0;
    _modifierKey.gameObject.SetActive(!symbol);
    _modifierSymbol.gameObject.SetActive(symbol);
    _modifierSide.gameObject.SetActive(symbol);
    _modifierSymbol.SetKey(_settings.SettingsShortcutModifier);
    _modifierSide.text = IsRightModifier(_settings.SettingsShortcutModifier) ? "R" : "L";
    _mainKey.text = KeyLabel(_settings.SettingsShortcutKey);
    float firstWidth = modifier ? (symbol ? 58 : Mathf.Min(112, _modifierKey.preferredWidth + 20)) : 0;
    float gap = modifier ? 28 : 0;
    float naturalKeyWidth = Mathf.Max(44, _mainKey.preferredWidth + 20);
    float captureWidth = LayoutShortcutControls(firstWidth + gap + naturalKeyWidth + 26);
    float secondWidth = Mathf.Clamp(naturalKeyWidth, 44, Mathf.Max(44, captureWidth - 26 - firstWidth - gap));
    float start = 0;
    SettingsUiFactory.Place((RectTransform)_modifierKey.transform.parent, start, 0, firstWidth, 30);
    SettingsUiFactory.Place(_keyPlus.rectTransform, start + firstWidth + 4, 0, 20, 30);
    SettingsUiFactory.Place(
      (RectTransform)_mainKey.transform.parent,
      start + firstWidth + (modifier ? 28 : 0),
      0,
      secondWidth,
      30
    );
  }

  private static string ModifierLabel(KeyCode key)
  {
    if (IsMac && SettingsKeySymbol.Symbol(key).Length > 0)
      return (IsRightModifier(key) ? L("오른쪽 ", "Right ") : L("왼쪽 ", "Left ")) + SettingsKeySymbol.Symbol(key);
    string modifier = key.ToString()
      .Replace("Left", L("왼쪽 ", "Left "))
      .Replace("Right", L("오른쪽 ", "Right "))
      .Replace("Control", "Ctrl")
      .Replace("Command", "Cmd");
    if (
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor
    )
      modifier = modifier.Replace("Alt", "Option");
    return modifier;
  }

  public static string ShortcutLabel(ImplResourcePackSettings settings) =>
    (settings.SettingsShortcutModifier == KeyCode.None ? "" : ModifierLabel(settings.SettingsShortcutModifier) + " + ")
    + KeyLabel(settings.SettingsShortcutKey);

  private static bool IsMac =>
    UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
    || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;

  private static bool IsRightModifier(KeyCode key) =>
    key == KeyCode.RightAlt || key == KeyCode.RightCommand || key == KeyCode.RightControl || key == KeyCode.RightShift;

  internal static string KeyLabel(KeyCode key)
  {
    if (key >= KeyCode.A && key <= KeyCode.Z)
      return key.ToString().ToUpperInvariant();
    if ((int)key >= 33 && (int)key <= 126)
      return ((char)key).ToString();
    if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
      return "Num " + ((int)key - (int)KeyCode.Keypad0);
    return key switch
    {
      KeyCode.Escape => "Esc",
      KeyCode.Return => "Enter",
      KeyCode.Backspace => IsMac ? "Delete" : "Backspace",
      KeyCode.Delete => "Del",
      KeyCode.CapsLock => "Caps Lock",
      KeyCode.PageUp => "Page Up",
      KeyCode.PageDown => "Page Down",
      KeyCode.UpArrow => "↑",
      KeyCode.DownArrow => "↓",
      KeyCode.LeftArrow => "←",
      KeyCode.RightArrow => "→",
      KeyCode.KeypadPeriod => "Num .",
      KeyCode.KeypadDivide => "Num /",
      KeyCode.KeypadMultiply => "Num *",
      KeyCode.KeypadMinus => "Num -",
      KeyCode.KeypadPlus => "Num +",
      KeyCode.KeypadEnter => "Num Enter",
      KeyCode.KeypadEquals => "Num =",
      _ => key.ToString(),
    };
  }

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
      : _editingPreset == null ? L("새 프리셋", "New preset")
      : L("프리셋 편집", "Edit preset");
    _back.gameObject.SetActive(index != 0);
    FitWindow();
    Canvas.ForceUpdateCanvases();
    _scroll.StopMovement();
    _scroll.verticalNormalizedPosition = _scrollPositions[index];
    _lastFocused = null;
    RefreshPlanetPreview();
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
    _ownedEventSystem = new GameObject(
      "ImplResourcePack UI Events",
      typeof(EventSystem),
      typeof(StandaloneInputModule)
    );
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
