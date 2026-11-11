using Element.Tests.Support;

namespace Element.Tests.Domain.Overlay;

/// <summary>
/// Overlay elements, one file per part: the value catalog, migration of older
/// settings, saving, text and fonts, shapes, and formulas.
/// </summary>
internal static class OverlayElementTests
{
  [Suite("overlay elements: values, migration, text editing and shape geometry")]
  public static void Run()
  {
    OverlayValueCatalogTests.Run();
    OverlayMigrationTests.Run();
    OverlaySavingTests.Run();
    OverlayTextTests.Run();
    OverlayShapeTests.Run();
    OverlayFormulaTests.Run();
  }
}
