using System;
using System.Collections.Generic;

namespace Element.Configuration;

[Serializable]
[CopiedInPlace]
public sealed class OverlayTextSettings
{
  public List<OverlayTextRun> Runs = new();

  public OverlayTextSettings Clone()
  {
    OverlayTextSettings copy = new();
    if (Runs != null)
      foreach (OverlayTextRun run in Runs)
        if (run != null)
          copy.Runs.Add(run.Clone());
    return copy;
  }

  public bool SameAs(OverlayTextSettings other)
  {
    if (other == null)
      return false;
    int count = Runs?.Count ?? 0;
    if (count != (other.Runs?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Runs[i]?.SameAs(other.Runs[i]) ?? other.Runs[i] == null))
        return false;
    return true;
  }
}
