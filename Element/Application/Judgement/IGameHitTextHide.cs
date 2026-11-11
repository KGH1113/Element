namespace Element.Application.Judgement;

/// <summary>ADOFAI's own Hide Judgment Text setting.</summary>
internal interface IGameHitTextHide
{
  /// <summary>False outside Competitive Mode, where the game ignores the setting.</summary>
  bool Applies { get; }

  HitMarginHidePreset Value { get; set; }
}
