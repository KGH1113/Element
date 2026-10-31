using System;
using Element.Configuration;
using Element.Domain.Judgement;

namespace Element.Application.Judgement;

/// <summary>ADOFAI's own Hide Judgment Text setting.</summary>
internal interface IGameHitTextHide
{
  /// <summary>False outside Competitive Mode, where the game ignores the setting.</summary>
  bool Applies { get; }

  HitMarginHidePreset Value { get; set; }
}

/// <summary>
/// Keeps the judgments the mod hides and ADOFAI's Hide Judgment Text in agreement,
/// so both settings screens describe what the player sees. The game gets the
/// largest of its steps the mod covers; the mod hides any extra judgments itself.
/// Main thread only.
/// </summary>
internal sealed class HitTextHideSync
{
  private readonly Func<JudgementVisualOptions> _options;
  private readonly IGameHitTextHide _game;
  private readonly Action _changed;

  public HitTextHideSync(Func<JudgementVisualOptions> options, IGameHitTextHide game, Action changed)
  {
    _options = options;
    _game = game;
    _changed = changed;
  }

  public bool Started { get; private set; }

  /// <summary>
  /// Once the game has loaded its settings. A step the player picked in the game
  /// wins; with nothing picked there, the game follows the mod.
  /// </summary>
  public void Start()
  {
    if (Started)
      return;
    Started = true;
    if (!_game.Applies)
      return;
    HitMarginHidePreset step = _game.Value;
    if (step == HitMarginHidePreset.None)
      Push();
    else
      Pull(step);
  }

  /// <summary>The mod's choices changed, or Competitive Mode was switched.</summary>
  public void Push()
  {
    JudgementVisualOptions options = _options();
    if (!Started || options == null || !_game.Applies)
      return;
    HitMarginHidePreset covered = HitTextHideLadder.Covered(options);
    if (_game.Value != covered)
      _game.Value = covered;
  }

  /// <summary>The player picked a step in the game's menu: the mod hides exactly that step.</summary>
  public void PullFromGame()
  {
    if (Started && _game.Applies)
      Pull(_game.Value);
  }

  private void Pull(HitMarginHidePreset step)
  {
    JudgementVisualOptions options = _options();
    // Already agreeing keeps the extra judgments the mod hides on its own.
    if (options == null || !HitTextHideLadder.IsKnown(step) || HitTextHideLadder.Covered(options) == step)
      return;
    HitTextHideLadder.Apply(options, step);
    _changed();
  }
}
