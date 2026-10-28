using System;
using System.Collections.Generic;
using ImplResourcePack.Application.Updates;
using ImplResourcePack.Configuration;
using ImplResourcePack.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Settings;

internal sealed partial class SettingsPanel
{
  private readonly CanvasGroup _windowGroup;
  private readonly RectTransform _surface,
    _viewport,
    _shortcutSection;
  private readonly ScrollRect _scroll;
  private readonly Scrollbar _scrollbar;
  private readonly RectTransform _keycaps;
  private readonly RectTransform _captureFrame;
  private readonly SettingsKeySymbol _modifierSymbol;
  private readonly TextMeshProUGUI _modifierSide;
  private readonly TextMeshProUGUI _modifierKey,
    _mainKey,
    _keyPlus;
  private readonly Button _close,
    _back,
    _retrySave,
    _capture,
    _resetShortcut,
    _motionRow,
    _dmNoteRow;
  private readonly List<Button> _tweakRows = new();
  private readonly RectTransform[] _cardIcons = new RectTransform[4];
  private readonly TextMeshProUGUI[] _cardTitles = new TextMeshProUGUI[4];
  private readonly TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[4];
  private readonly TextMeshProUGUI[] _cardActions = new TextMeshProUGUI[4];
  private readonly RectTransform[] _cardDividers = new RectTransform[4];
  private readonly RectTransform[] _cardArrows = new RectTransform[4];
  private readonly float[] _scrollPositions = { 1, 1, 1, 1, 1, 1, 1, 1, 1 };
  private readonly SettingsWindowMotion _windowMotion;

  public SettingsPanel(
    ImplResourcePackSettings settings,
    TMP_FontAsset font,
    Action apply,
    Func<bool> save,
    Action editLayout,
    Action<bool> visibility,
    Func<int, PlanetAppearanceProfile, Texture> renderPlanetPreview = null,
    Func<long> blockedChatter = null,
    IModUpdates updates = null
  )
  {
    _settings = settings;
    _blockedChatter = blockedChatter;
    _updates = updates;
    settings.Gameplay.Normalize();
    settings.Effects.Normalize();
    _apply = apply;
    _save = save;
    _editLayout = editLayout;
    _visibility = visibility;
    _renderPlanetPreview = renderPlanetPreview;
    _ui = new SettingsUiFactory(font) { ReduceMotion = () => settings.ReduceUiMotion };
    _root = new GameObject("ImplResourcePack Settings", typeof(RectTransform));
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
    _back = _ui.IconButton("Back", _window, 32, 24, 40, Back, 4);
    _title = _ui.Text(L("설정", "Settings"), _window, 32, 24, 700, 40, 27);
    _close = _ui.IconButton("Close", _window, 928, 24, 40, Close, 5);
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
    string[] pages =
    {
      "Home",
      "Tweaks",
      "General",
      "Effects",
      "Editor tweaks",
      "Judgment visuals",
      "Planets",
      "Preset editor",
      "Chatter blocker",
    };
    for (int i = 0; i < _pages.Length; i++)
      _pages[i] = _ui.Rect(pages[i], _viewport, 0, 0, 936, 480).gameObject;
    CreateHomeCard(
      0,
      "Overlay",
      L("요소를 추가하고 색상과 배치를 편집합니다.", "Add elements and adjust their color and layout."),
      L("오버레이 설정", "Edit overlays"),
      () =>
      {
        if (!Commit())
          return;
        HideForLayout();
        _editLayout();
      }
    );
    CreateHomeCard(
      1,
      "Tweaks",
      L("판정 표시와 게임플레이 동작을 설정합니다.", "Adjust judgment display and gameplay behavior."),
      L("게임플레이 설정", "Open tweaks"),
      () => NavigateTo(1)
    );
    CreateHomeCard(
      2,
      "Effects",
      L("필요한 이펙트만 남기는 나만의 프리셋.", "Keep the effects you want with personal presets."),
      L("프리셋 선택", "Choose a preset"),
      () => NavigateTo(3)
    );
    CreateHomeCard(
      3,
      L("일반", "General"),
      L(
        "설정 단축키, UI 움직임, ImplDmNote 자동 실행, 업데이트를 설정합니다.",
        "Set your shortcut, motion, ImplDmNote and update preferences."
      ),
      L("일반 설정", "Open general"),
      () => NavigateTo(2)
    );
    CreateTweakPages();
    _tweakRows.Add(
      _ui.Switch(
        L("녹화 모드", "Record mode"),
        L("게임 HUD와 모드 오버레이를 숨깁니다.", "Hide the game HUD and mod overlays."),
        _pages[1].transform,
        100,
        () => settings.RecordMode,
        v =>
        {
          settings.RecordMode = v;
          Changed();
        }
      )
    );
    if (
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor
    )
      _tweakRows.Add(
        _ui.Switch(
          L("macOS 트랙패드", "macOS Trackpad"),
          L(
            "에디터에서 두 손가락으로 이동하고 핀치로 확대 · 축소합니다.",
            "In the editor, pan with two fingers and pinch to zoom."
          ),
          _pages[1].transform,
          200,
          () => settings.NativeEditorTrackpad,
          v =>
          {
            settings.NativeEditorTrackpad = v;
            Changed();
          }
        )
      );
    _shortcutSection = _ui.Panel(
      "Settings shortcut",
      _pages[2].transform,
      0,
      0,
      936,
      124,
      SettingsUiFactory.Surface
    ).rectTransform;
    TextMeshProUGUI shortcutTitle = _ui.Text(
      L("설정 단축키", "Settings shortcut"),
      _shortcutSection,
      16,
      16,
      700,
      28,
      17
    );
    SettingsUiFactory.StretchWidth(shortcutTitle.rectTransform, 16, 16);
    _captureFrame = _ui.Panel(
      "Shortcut input border",
      _shortcutSection,
      16,
      60,
      240,
      44,
      new Color(.3f, .3f, .3f)
    ).rectTransform;
    _capture = _ui.Button("", _captureFrame, 1, 1, 238, 42, BeginCapture);
    StyleShortcutButton(_capture);
    _shortcutLabel = _capture.GetComponentInChildren<TextMeshProUGUI>();
    _keycaps = _ui.Rect("Shortcut keys", _capture.transform, 12, 6, 214, 30);
    SettingsUiFactory.StretchWidth(_keycaps, 12, 12);
    Image modifierCap = _ui.Panel("Modifier", _keycaps, 0, 0, 136, 30, new Color(.21f, .21f, .21f));
    _modifierKey = _ui.Text("", modifierCap.transform, 8, 0, 120, 30, 15);
    SettingsUiFactory.Stretch(_modifierKey.rectTransform, 8, 0, 8, 0);
    _modifierKey.alignment = TextAlignmentOptions.Midline;
    _modifierSide = _ui.Text("L", modifierCap.transform, 8, 0, 14, 30, 11, SettingsUiFactory.Muted);
    _modifierSymbol = _ui.Rect("Modifier symbol", modifierCap.transform, 28, 4, 22, 22)
      .gameObject.AddComponent<SettingsKeySymbol>();
    _modifierSymbol.color = SettingsUiFactory.Foreground;
    _modifierSymbol.raycastTarget = false;
    _keyPlus = _ui.Text("+", _keycaps, 144, 0, 20, 30, 14, SettingsUiFactory.Muted);
    _keyPlus.alignment = TextAlignmentOptions.Midline;
    Image mainCap = _ui.Panel("Key", _keycaps, 172, 0, 80, 30, new Color(.21f, .21f, .21f));
    _mainKey = _ui.Text("", mainCap.transform, 8, 0, 64, 30, 15);
    SettingsUiFactory.Stretch(_mainKey.rectTransform, 8, 0, 8, 0);
    _mainKey.alignment = TextAlignmentOptions.Midline;
    _resetShortcut = _ui.Button(
      "",
      _shortcutSection,
      328,
      60,
      44,
      44,
      () =>
      {
        settings.SettingsShortcutKey = KeyCode.Tab;
        settings.SettingsShortcutModifier = KeyCode.LeftAlt;
        CancelCapture();
        Changed();
      }
    );
    _resetShortcut.gameObject.name = "Reset shortcut";
    _resetShortcut.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    LayoutActionIcon resetIcon = _ui.Rect("Reset icon", _resetShortcut.transform, 10, 10, 24, 24)
      .gameObject.AddComponent<LayoutActionIcon>();
    resetIcon.Kind = LayoutAction.Reset;
    resetIcon.color = SettingsUiFactory.Foreground;
    resetIcon.raycastTarget = false;
    StyleShortcutButton(_resetShortcut);
    _captureHint = _ui.Text("", _shortcutSection, 16, 114, 904, 48, 14, SettingsUiFactory.Muted);
    SettingsUiFactory.StretchWidth(_captureHint.rectTransform, 16, 16);
    _motionRow = _ui.Switch(
      L("움직임 줄이기", "Reduce motion"),
      L("설정 전환과 스위치 움직임을 즉시 적용합니다.", "Use immediate menu and switch transitions."),
      _pages[2].transform,
      136,
      () => settings.ReduceUiMotion,
      v =>
      {
        settings.ReduceUiMotion = v;
        Changed();
      }
    );
    _dmNoteRow = _ui.Switch(
      L("ImplDmNote 자동 열기/닫기", "Auto open/close ImplDmNote"),
      L(
        "게임을 켜면 ImplDmNote를 열고, 게임을 끄면 함께 닫습니다.",
        "Open ImplDmNote with the game and close it when the game quits."
      ),
      _pages[2].transform,
      236,
      () => settings.AutoOpenCloseDmNote,
      v =>
      {
        settings.AutoOpenCloseDmNote = v;
        Changed();
      }
    );
    CreateUpdateRow(336);
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
    CreateEffectsPages(font);
    CreatePlanetPopover(font);
    SelectPage(0);
    FitWindow();
    UpdateShortcutLabel();
    _root.SetActive(false);
  }

  private void CreateHomeCard(int index, string title, string description, string action, Action onClick)
  {
    Button card = _ui.Button(title, _pages[0].transform, 0, 0, 300, 280, onClick);
    card.gameObject.name = title + " category";
    card.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _homeCards[index] = card;
    SettingsNavigationIcon icon = _ui.Rect("Category icon", card.transform, 24, 24, 36, 36)
      .gameObject.AddComponent<SettingsNavigationIcon>();
    icon.Kind =
      index == 2 ? 6
      : index == 3 ? 2
      : index;
    icon.raycastTarget = false;
    _cardIcons[index] = icon.rectTransform;
    _cardTitles[index] = _ui.Text(title, card.transform, 24, 84, 252, 36, 25);
    _cardDescriptions[index] = _ui.Text(description, card.transform, 24, 136, 252, 68, 15, SettingsUiFactory.Muted);
    _cardDescriptions[index].alignment = TextAlignmentOptions.TopLeft;
    _cardDividers[index] = _ui.Panel(
      "Action divider",
      card.transform,
      24,
      224,
      252,
      1,
      new Color(.23f, .23f, .23f),
      false
    ).rectTransform;
    _cardActions[index] = _ui.Text(action, card.transform, 24, 239, 224, 24, 15, _ui.SoftAccent);
    SettingsNavigationIcon arrow = _ui.Rect("Open category", card.transform, 260, 240, 16, 24)
      .gameObject.AddComponent<SettingsNavigationIcon>();
    arrow.Kind = 3;
    arrow.raycastTarget = false;
    _cardArrows[index] = arrow.rectTransform;
    _ui.Bind(() =>
    {
      icon.color = SettingsUiFactory.Muted;
      arrow.color = _ui.SoftAccent;
      _cardActions[index].color = _ui.SoftAccent;
    });
  }

  private void NavigateTo(int page)
  {
    SelectPage(page);
    EventSystem.current?.SetSelectedGameObject(_back.gameObject);
  }

  private void ClearFeedback()
  {
    _feedback.text = "";
    _retrySave.gameObject.SetActive(false);
    FitWindow();
  }

  private void FitWindow() => Resize(Screen.width, Screen.height);

  internal void Resize(int pixelWidth, int pixelHeight)
  {
    if (_feedback == null)
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
    float titleLeft = _page == 0 ? inset : inset + 52;
    SettingsUiFactory.Place((RectTransform)_back.transform, inset, 24, 40, 40);
    SettingsUiFactory.Place(_title.rectTransform, titleLeft, 24, width - titleLeft - inset - 48, 40);
    SettingsUiFactory.Place((RectTransform)_close.transform, width - inset - 40, 24, 40, 40);
    float bottom = string.IsNullOrEmpty(_feedback.text) ? 24 : 80;
    float viewHeight = Mathf.Max(80, height - 96 - bottom);
    SettingsUiFactory.Place(_viewport, inset, 96, contentWidth, viewHeight);
    SettingsUiFactory.Place((RectTransform)_scrollbar.transform, width - inset + 6, 96, 12, viewHeight);
    bool stacked = contentWidth < 480;
    float cardWidth = stacked ? contentWidth : (contentWidth - 16) / 2;
    float cardHeight = stacked ? 172 : Mathf.Clamp((viewHeight - 16) / 2, 192, 216);
    float homeHeight = stacked ? cardHeight * 4 + 36 : cardHeight * 2 + 16;
    ((RectTransform)_pages[0].transform).sizeDelta = new Vector2(contentWidth, homeHeight);
    for (int i = 0; i < _homeCards.Length; i++)
    {
      SettingsUiFactory.Place(
        (RectTransform)_homeCards[i].transform,
        stacked ? 0 : i % 2 * (cardWidth + 16),
        stacked ? i * (cardHeight + 12) : i / 2 * (cardHeight + 16),
        cardWidth,
        cardHeight
      );
      SettingsUiFactory.Place(_cardIcons[i], 20, 20, 32, 32);
      SettingsUiFactory.Place(_cardTitles[i].rectTransform, 68, 18, cardWidth - 88, 36);
      SettingsUiFactory.Place(_cardDescriptions[i].rectTransform, 20, 66, cardWidth - 40, cardHeight - 126);
      SettingsUiFactory.Place(_cardDividers[i], 20, cardHeight - 52, cardWidth - 40, 1);
      SettingsUiFactory.Place(_cardActions[i].rectTransform, 20, cardHeight - 38, cardWidth - 66, 24);
      SettingsUiFactory.Place(_cardArrows[i], cardWidth - 36, cardHeight - 38, 16, 24);
      _homeCards[i].navigation = new Navigation
      {
        mode = Navigation.Mode.Explicit,
        selectOnLeft = !stacked && i % 2 == 1 ? _homeCards[i - 1] : null,
        selectOnRight = !stacked && i % 2 == 0 ? _homeCards[i + 1] : null,
        selectOnUp = i >= (stacked ? 1 : 2) ? _homeCards[i - (stacked ? 1 : 2)] : _close,
        selectOnDown = i + (stacked ? 1 : 2) < 4 ? _homeCards[i + (stacked ? 1 : 2)] : null,
      };
    }
    float rowHeight = contentWidth < 520 ? 116 : 88;
    LayoutTweakPages(contentWidth);
    LayoutEffectsPages(contentWidth);
    LayoutPopover();
    SettingsUiFactory.Place(_shortcutSection, 0, 0, contentWidth, 124);
    if (_capturing)
      LayoutShortcutControls(_shortcutLabel.preferredWidth + 26);
    else
      UpdateShortcutLabel();
    SettingsUiFactory.Place(_feedback.rectTransform, inset, height - 64, contentWidth - 132, 48);
    SettingsUiFactory.Place((RectTransform)_retrySave.transform, width - inset - 120, height - 60, 120, 40);
  }

  private float LayoutShortcutControls(float desiredWidth)
  {
    float contentWidth = _shortcutSection.rect.width;
    float captureWidth = Mathf.Min(desiredWidth, contentWidth - 32);
    bool stackShortcut = captureWidth + 52 > contentWidth - 32;
    float shortcutHeight = (_capturing ? 178 : 124) + (stackShortcut ? 56 : 0);
    _shortcutSection.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, shortcutHeight);
    SettingsUiFactory.Place(_captureFrame, 16, 60, captureWidth, 44);
    SettingsUiFactory.Place((RectTransform)_capture.transform, 1, 1, captureWidth - 2, 42);
    SettingsUiFactory.Place(
      (RectTransform)_resetShortcut.transform,
      stackShortcut ? 16 : captureWidth + 24,
      stackShortcut ? 116 : 60,
      44,
      44
    );
    SettingsUiFactory.Place(_captureHint.rectTransform, 16, stackShortcut ? 170 : 114, contentWidth - 32, 48);
    float rowHeight = contentWidth < 520 ? 116 : 88;
    SettingsUiFactory.Place((RectTransform)_motionRow.transform, 0, shortcutHeight + 12, contentWidth, rowHeight);
    float dmNoteTop = shortcutHeight + 12 + rowHeight + 12;
    SettingsUiFactory.Place((RectTransform)_dmNoteRow.transform, 0, dmNoteTop, contentWidth, rowHeight);
    float bottom = dmNoteTop + rowHeight;
    if (_updateRow != null)
    {
      SettingsUiFactory.Place((RectTransform)_updateRow.transform, 0, bottom + 12, contentWidth, rowHeight);
      bottom += 12 + rowHeight;
    }
    ((RectTransform)_pages[2].transform).sizeDelta = new Vector2(contentWidth, bottom);
    return captureWidth;
  }

  private void StyleShortcutButton(Button button)
  {
    _ui.Bind(() =>
    {
      ColorBlock colors = button.colors;
      colors.normalColor = colors.selectedColor = new Color(.075f, .075f, .075f);
      colors.highlightedColor = new Color(.16f, .16f, .16f);
      colors.pressedColor = new Color(.05f, .05f, .05f);
      ((SettingsButton)button).SetPalette(colors);
    });
  }
}
