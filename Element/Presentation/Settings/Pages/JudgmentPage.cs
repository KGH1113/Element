using System;
using System.Collections.Generic;
using Element.Configuration.Gameplay;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks → Judgment visuals: a tile for each judgment popup, shown or hidden.</summary>
internal sealed class JudgmentPage : SettingsPage
{
  private readonly List<Button> _judgementOptions = new();
  private readonly RectTransform _judgementHeader;

  /// <param name="textLinked">True while ADOFAI's Competitive Mode is on and its Hide Judgment Text follows this page; null reads as off.</param>
  public JudgmentPage(ISettingsWindow window, Func<bool> textLinked)
    : base(window, "Judgment visuals")
  {
    JudgementVisualOptions j = Settings.Gameplay.Judgement;
    _judgementHeader = Ui.Rect("Judgment controls", Root, 0, 0, 936, 52);
    var hint = Ui.Text("", _judgementHeader, 0, 0, 580, 40, 15, UiPalette.Muted);
    SettingsUiFactory.StretchWidth(hint.rectTransform, 0, 232);
    // Competitive Mode changes in the game's pause menu; read it each time the window opens.
    Ui.Bind(() =>
      hint.text =
        textLinked?.Invoke() == true
          ? L(
            "표시할 판정을 선택하세요. 얼불춤의 판정 텍스트 숨기기와 함께 바뀌어요.",
            "Choose the popups to show. Synced with ADOFAI's Hide Judgment Text."
          )
          : L(
            "표시할 판정을 선택하세요. 경쟁 모드를 켜면 얼불춤 설정과 함께 바뀌어요.",
            "Choose the popups to show. Turn on Competitive Mode to sync with ADOFAI."
          )
    );
    Ui.Button(L("모두 표시", "Show all"), _judgementHeader, 704, 0, 108, 40, () => SetAllJudgements(false));
    Ui.Button(L("모두 숨김", "Hide all"), _judgementHeader, 824, 0, 108, 40, () => SetAllJudgements(true));
    JudgmentOption("Early Perfect", new Color(.7f, .9f, .35f), () => j.HideEarlyPerfect, v => j.HideEarlyPerfect = v);
    JudgmentOption("Perfect - / +", new Color(.7f, .9f, .35f), () => j.HidePerfect, v => j.HidePerfect = v);
    JudgmentOption("XPerfect", new Color(.5f, .9f, .4f), () => j.HideXPerfect, v => j.HideXPerfect = v);
    JudgmentOption("Late Perfect", new Color(.7f, .9f, .35f), () => j.HideLatePerfect, v => j.HideLatePerfect = v);
    JudgmentOption("Too Early", new Color(1, .36f, .3f), () => j.HideTooEarly, v => j.HideTooEarly = v);
    JudgmentOption("Early", new Color(1, .6f, .4f), () => j.HideEarly, v => j.HideEarly = v);
    JudgmentOption("Late", new Color(1, .6f, .4f), () => j.HideLate, v => j.HideLate = v);
    JudgmentOption("Too Late", new Color(1, .36f, .3f), () => j.HideTooLate, v => j.HideTooLate = v);
    JudgmentOption("Miss", new Color(.85f, .5f, 1), () => j.HideMiss, v => j.HideMiss = v);
    JudgmentOption("Overload", new Color(.85f, .5f, 1), () => j.HideOverload, v => j.HideOverload = v);
  }

  public override string Title => L("판정 표시", "Judgment visuals");

  private void JudgmentOption(string title, Color tint, Func<bool> hidden, Action<bool> write)
  {
    Button tile = Ui.Button(
      title,
      Root,
      0,
      0,
      300,
      86,
      () =>
      {
        write(!hidden());
        Window.Changed();
      }
    );
    tile.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    Ui.Capsule("Judgment color", tile.transform, 16, 21, 8, 8, tint);
    var name = Ui.Text(title, tile.transform, 36, 12, 244, 28, 16);
    SettingsUiFactory.StretchWidth(name.rectTransform, 36, 16);
    var state = Ui.Text("", tile.transform, 36, 46, 244, 24, 13);
    SettingsUiFactory.StretchWidth(state.rectTransform, 36, 16);
    Ui.Bind(() =>
    {
      state.text = hidden() ? L("숨김", "Hidden") : L("표시", "Shown");
      state.color = hidden() ? UiPalette.Muted : Ui.SoftAccent;
      name.color = hidden() ? UiPalette.Muted : UiPalette.Foreground;
    });
    _judgementOptions.Add(tile);
  }

  private void SetAllJudgements(bool hidden)
  {
    var j = Settings.Gameplay.Judgement;
    j.HidePerfect =
      j.HideXPerfect =
      j.HideEarlyPerfect =
      j.HideLatePerfect =
      j.HideTooEarly =
      j.HideEarly =
      j.HideLate =
      j.HideTooLate =
      j.HideMiss =
      j.HideOverload =
        hidden;
    Window.Changed();
  }

  public override void Layout(float width, float height)
  {
    bool compact = width < 600;
    SettingsUiFactory.Place(_judgementHeader, 0, 0, width, compact ? 92 : 52);
    TextMeshProUGUI hint = _judgementHeader.GetComponentInChildren<TextMeshProUGUI>();
    SettingsUiFactory.Place(hint.rectTransform, 0, 0, compact ? width : width - 244, 40);
    Button[] actions = _judgementHeader.GetComponentsInChildren<Button>();
    for (int i = 0; i < actions.Length; i++)
      SettingsUiFactory.Place(
        (RectTransform)actions[i].transform,
        compact ? i * 120 : width - 232 + i * 120,
        compact ? 44 : 0,
        108,
        40
      );
    int count =
      width >= 660 ? 3
      : width >= 440 ? 2
      : 1;
    float judgmentWidth = (width - (count - 1) * 12) / count;
    for (int i = 0; i < _judgementOptions.Count; i++)
      SettingsUiFactory.Place(
        (RectTransform)_judgementOptions[i].transform,
        i % count * (judgmentWidth + 12),
        _judgementHeader.rect.height + i / count * 98,
        judgmentWidth,
        86
      );
    Root.sizeDelta = new Vector2(
      width,
      _judgementHeader.rect.height + Mathf.CeilToInt((float)_judgementOptions.Count / count) * 98 - 12
    );
  }
}
