using Element.Domain.Judgement;

namespace Element.Application.Judgement;

/// <summary>Reads the judgement counts of the single player's play from the game; Infrastructure implements it.</summary>
internal interface IJudgementSource
{
  /// <summary>False in co-op or outside a play.</summary>
  bool TryRead(out JudgementSnapshot snapshot);
}
