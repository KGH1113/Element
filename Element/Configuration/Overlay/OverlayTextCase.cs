using System;
using System.Collections.Generic;

namespace Element.Configuration.Overlay;

// One case of an If in a text: while When holds, the If shows these runs.
// No runs shows nothing, which hides that part of the text.
[Serializable]
[CopiedInPlace]
public sealed class OverlayTextCase
{
  public OverlayCondition When = new();
  public List<OverlayTextRun> Runs = new();

  public OverlayTextCase Clone() =>
    new() { When = (When ?? new OverlayCondition()).Clone(), Runs = OverlayTextSettings.CloneRuns(Runs) };

  public bool SameAs(OverlayTextCase other) =>
    other != null
    && (When ?? new OverlayCondition()).SameAs(other.When ?? new OverlayCondition())
    && OverlayTextSettings.SameRuns(Runs, other.Runs);
}
