using ImplResourcePack.Domain.Bpm;
using ImplResourcePack.Domain.Judgement;
using ImplResourcePack.Domain.Status;
using ImplResourcePack.Domain.TimingScale;

namespace ImplResourcePack.Application;

// Overlay features read the game through these ports; Infrastructure implements
// them, so the feature logic does not depend on how ADOFAI state is read.

internal interface IBpmSource
{
  BpmSnapshot Read();
}

internal interface IJudgementSource
{
  bool TryRead(out JudgementSnapshot snapshot);
  bool TryRead(scrMarginTracker tracker, out JudgementSnapshot snapshot);
}

internal interface IStatusSource
{
  void ResetRunState();
  StatusSnapshot ReadMetrics(StatusSnapshot current);
  StatusSnapshot ReadTime(StatusSnapshot current, int? framesPerSecond = null);
}

internal interface ITimingScaleSource
{
  TimingScaleSnapshot Read();
}
