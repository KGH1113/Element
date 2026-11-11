using System;
using System.Linq;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Values;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Domain.Overlay;

/// <summary>Every built-in value has one key and one place in the catalog.</summary>
internal static class OverlayValueCatalogTests
{
  internal static void Run()
  {
    ValueCatalogKeysAreUniqueAndIndexed();
  }

  private static void ValueCatalogKeysAreUniqueAndIndexed()
  {
    Check(OverlayValueCatalog.BuiltIn.Count == OverlayValueCatalog.BuiltInCount, "every value id has a definition");
    Check(OverlayValueCatalog.BuiltInCount < OverlayValueCatalog.Capacity, "value masks leave room for other mods");
    Check(
      OverlayValueCatalog.BuiltIn.Select(value => value.Key).Distinct().Count() == OverlayValueCatalog.BuiltInCount,
      "value keys are unique"
    );
    foreach (OverlayValueDefinition value in OverlayValueCatalog.BuiltIn)
    {
      Check(OverlayValueCatalog.TryGet(value.Key, out OverlayValueDefinition found) && found == value, value.Key);
      Check(!string.IsNullOrEmpty(value.Sample), value.Key + " has a sample for the editor");
    }
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
    foreach (OverlayTextRun run in element.CreateRuns())
      Check(!run.IsValue || OverlayValueCatalog.TryGet(run.Value, out _), element.Id + " uses known values");
  }
}
