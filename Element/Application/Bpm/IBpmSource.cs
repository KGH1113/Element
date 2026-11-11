using Element.Domain.Bpm;

namespace Element.Application.Bpm;

/// <summary>Reads the BPM from the game; Infrastructure implements it.</summary>
internal interface IBpmSource
{
  BpmSnapshot Read();
}
