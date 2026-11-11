using Element.Domain.Judgement;

namespace Element.Application.Judgement;

/// <summary>Reads the timing scale from the game; Infrastructure implements it.</summary>
internal interface ITimingScaleSource
{
  TimingScaleSnapshot Read();
}
