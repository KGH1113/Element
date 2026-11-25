using System;
using System.Collections.Generic;

namespace Element.Configuration.Overlay;

// An If in a text: a part of the text that looks different from case to
// case. The cases are read from the first; the first whose condition holds
// shows its runs, and with none holding, Otherwise shows. The runs inside
// hold no Ifs of their own.
[Serializable]
[CopiedInPlace]
public sealed class OverlayTextIf
{
  public const int MaximumCases = 8;

  public List<OverlayTextCase> Cases = new();
  public List<OverlayTextRun> Otherwise = new();

  public OverlayTextIf Clone()
  {
    OverlayTextIf copy = new() { Otherwise = OverlayTextSettings.CloneRuns(Otherwise) };
    if (Cases != null)
      foreach (OverlayTextCase item in Cases)
        if (item != null)
          copy.Cases.Add(item.Clone());
    return copy;
  }

  public bool SameAs(OverlayTextIf other)
  {
    if (other == null || !OverlayTextSettings.SameRuns(Otherwise, other.Otherwise))
      return false;
    int count = Cases?.Count ?? 0;
    if (count != (other.Cases?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Cases[i]?.SameAs(other.Cases[i]) ?? other.Cases[i] == null))
        return false;
    return true;
  }

  /// <summary>Both none, or both the same If.</summary>
  public static bool Same(OverlayTextIf a, OverlayTextIf b) => a == null ? b == null : a.SameAs(b);
}
