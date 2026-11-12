using System;
using System.Linq.Expressions;
using Element.Configuration.Gameplay;
using Element.Presentation.Settings.Window;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>Tweaks → Judgment visuals: a tile for each judgment popup, shown or hidden.</summary>
internal sealed class JudgmentModel : SettingsPageModel
{
  private static readonly Color PerfectTint = new(.7f, .9f, .35f),
    PurePerfectTint = new(.5f, .9f, .4f),
    TooEarlyOrLateTint = new(1, .36f, .3f),
    EarlyOrLateTint = new(1, .6f, .4f),
    FailureTint = new(.85f, .5f, 1);

  /// <summary>The ten popups, in the order the tiles show them.</summary>
  public readonly JudgmentChoice[] Choices;

  /// <summary>
  /// True while ADOFAI's Competitive Mode ties its Hide Judgment Text to this
  /// page. It changes in the game's pause menu, so it is read when the window opens.
  /// </summary>
  public bool TextLinked;

  public Command ShowAll { get; }
  public Command HideAll { get; }

  public JudgmentModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("판정 표시", "Judgment visuals"))
  {
    ShowAll = Command(() => SetAll(false));
    HideAll = Command(() => SetAll(true));
    Choices = new[]
    {
      Choice(
        "Early Perfect",
        PerfectTint,
        () => Settings.Gameplay.Judgement.HideEarlyPerfect,
        hidden => Settings.Gameplay.Judgement.HideEarlyPerfect = hidden
      ),
      Choice(
        "Perfect - / +",
        PerfectTint,
        () => Settings.Gameplay.Judgement.HidePerfect,
        hidden => Settings.Gameplay.Judgement.HidePerfect = hidden
      ),
      Choice(
        "XPerfect",
        PurePerfectTint,
        () => Settings.Gameplay.Judgement.HideXPerfect,
        hidden => Settings.Gameplay.Judgement.HideXPerfect = hidden
      ),
      Choice(
        "Late Perfect",
        PerfectTint,
        () => Settings.Gameplay.Judgement.HideLatePerfect,
        hidden => Settings.Gameplay.Judgement.HideLatePerfect = hidden
      ),
      Choice(
        "Too Early",
        TooEarlyOrLateTint,
        () => Settings.Gameplay.Judgement.HideTooEarly,
        hidden => Settings.Gameplay.Judgement.HideTooEarly = hidden
      ),
      Choice(
        "Early",
        EarlyOrLateTint,
        () => Settings.Gameplay.Judgement.HideEarly,
        hidden => Settings.Gameplay.Judgement.HideEarly = hidden
      ),
      Choice(
        "Late",
        EarlyOrLateTint,
        () => Settings.Gameplay.Judgement.HideLate,
        hidden => Settings.Gameplay.Judgement.HideLate = hidden
      ),
      Choice(
        "Too Late",
        TooEarlyOrLateTint,
        () => Settings.Gameplay.Judgement.HideTooLate,
        hidden => Settings.Gameplay.Judgement.HideTooLate = hidden
      ),
      Choice(
        "Miss",
        FailureTint,
        () => Settings.Gameplay.Judgement.HideMiss,
        hidden => Settings.Gameplay.Judgement.HideMiss = hidden
      ),
      Choice(
        "Overload",
        FailureTint,
        () => Settings.Gameplay.Judgement.HideOverload,
        hidden => Settings.Gameplay.Judgement.HideOverload = hidden
      ),
    };
  }

  private JudgmentChoice Choice(string name, Color tint, Expression<Func<bool>> hidden, Action<bool> write)
  {
    Computed<bool> isHidden = Computed(hidden);
    return new JudgmentChoice(name, tint, isHidden, Command(() => write(!isHidden.Value)));
  }

  private void SetAll(bool hidden)
  {
    JudgementVisualOptions j = Settings.Gameplay.Judgement;
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
  }

  public override void Opened() => ReadTextLink();

  public override void Shown() => ReadTextLink();

  private void ReadTextLink() => TextLinked = Window.Features.JudgementTextLinked?.Invoke() == true;
}
