using System;
using System.Collections.Generic;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Tests.Domain.Overlay;

/// <summary>What the overlay element tests share.</summary>
internal static class OverlayFixtures
{
  internal static double Evaluate(string text, Numbers numbers, out bool time)
  {
    if (!OverlayFormula.Parse(text).TryEvaluate(numbers, out double result, out time))
      throw new InvalidOperationException(text + " should calculate");
    return result;
  }

  internal sealed class Numbers : Dictionary<OverlayValueId, double>, IOverlayNumberSource
  {
    public bool TryGetNumber(OverlayValueId id, out double number) => TryGetValue(id, out number);
  }

  internal static string Describe(List<OverlayTextRun> runs) =>
    string.Concat(
      runs.Select(run =>
        run.IsValue
          ? "[" + run.Value + "|" + (run.Color.Highlight ? "highlight" : "custom") + "]"
          : "'" + run.Text + "'"
      )
    );

  internal static OverlayLayoutSettings Find(List<OverlayLayoutSettings> layouts, string id) =>
    layouts.Find(layout => layout.Id == id);
}
