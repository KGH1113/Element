using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>The first page: a card for each part of the settings, the overlay editor first.</summary>
internal sealed class HomePage : SettingsPage
{
  private readonly Selectable _close;
  private readonly Button[] _homeCards = new Button[4];
  private readonly RectTransform[] _cardIcons = new RectTransform[4];
  private readonly TextMeshProUGUI[] _cardTitles = new TextMeshProUGUI[4];
  private readonly TextMeshProUGUI[] _cardDescriptions = new TextMeshProUGUI[4];
  private readonly TextMeshProUGUI[] _cardActions = new TextMeshProUGUI[4];
  private readonly RectTransform[] _cardDividers = new RectTransform[4];
  private readonly RectTransform[] _cardArrows = new RectTransform[4];

  /// <param name="close">The window's Close, above the first cards.</param>
  public HomePage(
    ISettingsWindow window,
    Selectable close,
    TweaksPage tweaks,
    EffectsPage effects,
    GeneralPage general,
    bool profiles
  )
    : base(window, "Home")
  {
    _close = close;
    CreateCard(
      0,
      SettingsNavigationIconKind.Overlay,
      "Overlay",
      L("요소를 추가하고 색상과 배치를 편집합니다.", "Add elements and adjust their color and layout."),
      L("오버레이 설정", "Edit overlays"),
      Window.EditLayout
    );
    CreateCard(
      1,
      SettingsNavigationIconKind.Tweaks,
      "Tweaks",
      L("판정 표시와 게임플레이 동작을 설정합니다.", "Adjust judgment display and gameplay behavior."),
      L("게임플레이 설정", "Open tweaks"),
      () => Window.OpenPage(tweaks)
    );
    Adopt(tweaks, _homeCards[1]);
    CreateCard(
      2,
      SettingsNavigationIconKind.Effects,
      "Effects",
      L("필요한 이펙트만 남기는 나만의 프리셋.", "Keep the effects you want with personal presets."),
      L("프리셋 선택", "Choose a preset"),
      () => Window.OpenPage(effects)
    );
    Adopt(effects, _homeCards[2]);
    CreateCard(
      3,
      SettingsNavigationIconKind.General,
      L("일반", "General"),
      profiles
        ? L(
          "프로필, 설정 단축키, UI 움직임, ElementNote 자동 실행, 업데이트를 설정합니다.",
          "Profiles, your shortcut, motion, ElementNote and update preferences."
        )
        : L(
          "설정 단축키, UI 움직임, ElementNote 자동 실행, 업데이트를 설정합니다.",
          "Set your shortcut, motion, ElementNote and update preferences."
        ),
      L("일반 설정", "Open general"),
      () => Window.OpenPage(general)
    );
    Adopt(general, _homeCards[3]);
  }

  public override string Title => L("설정", "Settings");

  private void CreateCard(
    int index,
    SettingsNavigationIconKind kind,
    string title,
    string description,
    string action,
    Action onClick
  )
  {
    Button card = Ui.Button(title, Root, 0, 0, 300, 280, onClick);
    card.gameObject.name = title + " category";
    card.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    _homeCards[index] = card;
    SettingsNavigationIcon icon = Ui.Rect("Category icon", card.transform, 24, 24, 36, 36)
      .gameObject.AddComponent<SettingsNavigationIcon>();
    icon.Kind = kind;
    icon.raycastTarget = false;
    _cardIcons[index] = icon.rectTransform;
    _cardTitles[index] = Ui.Text(title, card.transform, 24, 84, 252, 36, 25);
    _cardDescriptions[index] = Ui.Text(description, card.transform, 24, 136, 252, 68, 15, SettingsUiFactory.Muted);
    _cardDescriptions[index].alignment = TextAlignmentOptions.TopLeft;
    _cardDividers[index] = Ui.Panel(
      "Action divider",
      card.transform,
      24,
      224,
      252,
      1,
      new Color(.23f, .23f, .23f),
      false
    ).rectTransform;
    _cardActions[index] = Ui.Text(action, card.transform, 24, 239, 224, 24, 15, Ui.SoftAccent);
    SettingsNavigationIcon arrow = Ui.Rect("Open category", card.transform, 260, 240, 16, 24)
      .gameObject.AddComponent<SettingsNavigationIcon>();
    arrow.Kind = SettingsNavigationIconKind.Forward;
    arrow.raycastTarget = false;
    _cardArrows[index] = arrow.rectTransform;
    Ui.Bind(() =>
    {
      icon.color = SettingsUiFactory.Muted;
      arrow.color = Ui.SoftAccent;
      _cardActions[index].color = Ui.SoftAccent;
    });
  }

  public override void Layout(float width, float height)
  {
    bool stacked = width < 480;
    float cardWidth = stacked ? width : (width - 16) / 2;
    float cardHeight = stacked ? 172 : Mathf.Clamp((height - 16) / 2, 192, 216);
    float homeHeight = stacked ? cardHeight * 4 + 36 : cardHeight * 2 + 16;
    Root.sizeDelta = new Vector2(width, homeHeight);
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
  }
}
