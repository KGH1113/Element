using Element.Domain.Attempts;

namespace Element.Application.Attempts;

/// <summary>Reads the play the game just started.</summary>
internal interface IAttemptRunSource
{
  bool TryRead(int startFloor, out AttemptRun run);
}
