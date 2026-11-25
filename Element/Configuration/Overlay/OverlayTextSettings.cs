using System;
using System.Collections.Generic;

namespace Element.Configuration.Overlay;

[Serializable]
[CopiedInPlace]
public sealed class OverlayTextSettings
{
  public List<OverlayTextRun> Runs = new();

  public OverlayTextSettings Clone() => new() { Runs = CloneRuns(Runs) };

  public bool SameAs(OverlayTextSettings other) => other != null && SameRuns(Runs, other.Runs);

  /// <summary>
  /// Adds every run to <paramref name="output"/>, those inside each If's
  /// cases included (after the If itself), for what must reach them all: the
  /// fonts a text uses, for one.
  /// </summary>
  public static void Collect(List<OverlayTextRun> runs, List<OverlayTextRun> output)
  {
    if (runs == null)
      return;
    foreach (OverlayTextRun run in runs)
    {
      if (run == null)
        continue;
      output.Add(run);
      if (run.If == null)
        continue;
      if (run.If.Cases != null)
        foreach (OverlayTextCase item in run.If.Cases)
          Collect(item?.Runs, output);
      Collect(run.If.Otherwise, output);
    }
  }

  internal static List<OverlayTextRun> CloneRuns(List<OverlayTextRun> runs)
  {
    List<OverlayTextRun> copy = new();
    if (runs != null)
      foreach (OverlayTextRun run in runs)
        if (run != null)
          copy.Add(run.Clone());
    return copy;
  }

  internal static bool SameRuns(List<OverlayTextRun> a, List<OverlayTextRun> b)
  {
    int count = a?.Count ?? 0;
    if (count != (b?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(a[i]?.SameAs(b[i]) ?? b[i] == null))
        return false;
    return true;
  }
}
