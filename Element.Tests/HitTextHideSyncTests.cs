using System;
using Element.Application.Judgement;
using Element.Configuration;
using Element.Domain.Judgement;

namespace Element.Tests;

internal static class HitTextHideSyncTests
{
  public static void Run()
  {
    StepsMatchTheGame();
    CoveredStep();
    StartPrefersAPickedGameStep();
    StartKeepsExtraHiddenJudgments();
    PushAndPull();
    CompetitiveModeOff();
  }

  // Checked against the game's own rule, so a game update that changes its steps fails here.
  private static void StepsMatchTheGame()
  {
    foreach (HitMarginHidePreset step in Enum.GetValues(typeof(HitMarginHidePreset)))
    {
      JudgementVisualOptions options = new();
      HitTextHideLadder.Apply(options, step);
      Check(HitTextHideLadder.Covered(options) == step, "each game step round-trips through the mod's choices");
      foreach (HitMargin margin in Enum.GetValues(typeof(HitMargin)))
      {
        if (margin is HitMargin.Auto or HitMargin.Midspin)
          continue;
        Check(
          JudgementVisualPolicy.ShouldShow(options, false, (int)margin) == !margin.CheckHidden(step),
          "the mod hides exactly what ADOFAI hides for " + step + " (" + margin + ")"
        );
      }
    }
  }

  private static void CoveredStep()
  {
    JudgementVisualOptions options = Shown();
    options.HideMiss = true;
    Check(
      HitTextHideLadder.Covered(options) == HitMarginHidePreset.None,
      "a choice outside the game's steps leaves the game showing everything"
    );
    options.HideXPerfect = options.HidePerfect = true;
    Check(
      HitTextHideLadder.Covered(options) == HitMarginHidePreset.HidePerfect,
      "the game gets the largest step the mod covers"
    );
    Check(!HitTextHideLadder.IsKnown((HitMarginHidePreset)99), "a step from a newer game is not guessed at");
  }

  private static void StartPrefersAPickedGameStep()
  {
    FakeGame game = new() { Stored = HitMarginHidePreset.None };
    JudgementVisualOptions options = new();
    HitTextHideSync sync = New(options, game, out Func<int> changes);
    sync.Push();
    Check(game.Writes == 0, "nothing syncs before the game has loaded its settings");
    sync.Start();
    Check(
      game.Stored == HitMarginHidePreset.HidePerfect && changes() == 0,
      "with nothing picked in the game, the game follows the mod's defaults"
    );

    game = new() { Stored = HitMarginHidePreset.HideAll };
    options = new();
    sync = New(options, game, out changes);
    sync.Start();
    Check(
      HitTextHideLadder.Covered(options) == HitMarginHidePreset.HideAll && changes() == 1 && game.Writes == 0,
      "a step picked in the game wins at start and is saved once"
    );

    game = new() { Stored = (HitMarginHidePreset)99 };
    options = new();
    sync = New(options, game, out changes);
    sync.Start();
    Check(options.HidePerfect && changes() == 0 && game.Writes == 0, "an unknown game step changes nothing");
  }

  private static void StartKeepsExtraHiddenJudgments()
  {
    FakeGame game = new() { Stored = HitMarginHidePreset.HidePerfect };
    JudgementVisualOptions options = new() { HideMiss = true };
    HitTextHideSync sync = New(options, game, out Func<int> changes);
    sync.Start();
    Check(
      options.HideMiss && changes() == 0 && game.Writes == 0,
      "settings that already agree keep the judgments the mod hides on its own"
    );
  }

  private static void PushAndPull()
  {
    FakeGame game = new() { Stored = HitMarginHidePreset.HidePerfect };
    JudgementVisualOptions options = new();
    HitTextHideSync sync = New(options, game, out Func<int> changes);
    sync.Start();

    options.HideEarlyPerfect = options.HideLatePerfect = true;
    sync.Push();
    Check(game.Stored == HitMarginHidePreset.HideEarlyOrLatePerfect, "the mod's changes reach the game");
    int writes = game.Writes;
    sync.Push();
    Check(game.Writes == writes, "an unchanged step is not written again");

    options.HideMiss = true;
    game.Stored = HitMarginHidePreset.HideXPerfect;
    sync.PullFromGame();
    Check(
      HitTextHideLadder.Covered(options) == HitMarginHidePreset.HideXPerfect
        && !options.HidePerfect
        && !options.HideMiss
        && changes() == 1,
      "a step picked in the game's menu sets the mod to exactly that step"
    );

    game.Stored = HitMarginHidePreset.None;
    sync.PullFromGame();
    Check(!options.HideXPerfect && changes() == 2, "Don't Hide in the game shows every judgment");
  }

  private static void CompetitiveModeOff()
  {
    FakeGame game = new() { Applies = false, Stored = HitMarginHidePreset.HideAll };
    JudgementVisualOptions options = new();
    HitTextHideSync sync = New(options, game, out Func<int> changes);
    sync.Start();
    options.HideMiss = true;
    sync.Push();
    sync.PullFromGame();
    Check(
      game.Writes == 0 && game.Stored == HitMarginHidePreset.HideAll && changes() == 0 && !options.HideTooEarly,
      "outside Competitive Mode the game setting is left alone and the mod works by itself"
    );

    game.Applies = true;
    sync.Push();
    Check(
      game.Stored == HitMarginHidePreset.HidePerfect && options.HideMiss,
      "turning Competitive Mode on keeps what the player sees now"
    );
  }

  private static HitTextHideSync New(JudgementVisualOptions options, FakeGame game, out Func<int> changes)
  {
    int count = 0;
    changes = () => count;
    return new HitTextHideSync(() => options, game, () => count++);
  }

  private static JudgementVisualOptions Shown()
  {
    JudgementVisualOptions options = new();
    HitTextHideLadder.Apply(options, HitMarginHidePreset.None);
    return options;
  }

  // Reads like Persistence.hideHitMargin: the game reports None outside Competitive Mode.
  private sealed class FakeGame : IGameHitTextHide
  {
    public HitMarginHidePreset Stored;
    public int Writes;

    public bool Applies { get; set; } = true;

    public HitMarginHidePreset Value
    {
      get => Applies ? Stored : HitMarginHidePreset.None;
      set
      {
        Stored = value;
        Writes++;
      }
    }
  }

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
