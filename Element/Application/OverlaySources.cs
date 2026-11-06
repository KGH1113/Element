using Element.Domain.Attempts;
using Element.Domain.Bpm;
using Element.Domain.Judgement;
using Element.Domain.Status;
using Element.Domain.TimingScale;

namespace Element.Application;

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

internal interface IAttemptSource
{
  AttemptCounts Read();
}

/// <summary>
/// OrbitRender's video while it renders. The screen then runs slower than the
/// video and plays the level from the editor, so values that follow the screen
/// follow the video instead.
/// </summary>
internal interface IRenderVideo
{
  bool IsRendering { get; }
}
