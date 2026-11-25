using System;

namespace Element.Bootstrap.Hosting;

/// <summary>One piece of the mod's per-frame work, and what its failure is logged as.</summary>
internal readonly struct FrameTask
{
  public FrameTask(string context, Action tick)
  {
    Context = context;
    Tick = tick;
  }

  public string Context { get; }

  public Action Tick { get; }
}
