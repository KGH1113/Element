using Element.Domain.Status;

namespace Element.Application.Status;

/// <summary>Reads progress, accuracy, time and frame rate from the game; Infrastructure implements it.</summary>
internal interface IStatusSource
{
  void ResetRunState();
  StatusSnapshot ReadMetrics(StatusSnapshot current);
  StatusSnapshot ReadTime(StatusSnapshot current, int? framesPerSecond = null);
}
