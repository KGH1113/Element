using Element.Tests.Domain.KeyViewer;
using Element.Tests.Support;

namespace Element.Tests.Domain.Layout;

/// <summary>
/// Placing overlay elements, one file per part: anchored positions, how native
/// viewers grow, and resizing and moving by the handles.
/// </summary>
internal static class OverlayLayoutTests
{
  [Suite("overlay layout hit bounds and eight anchored proportional resize handles")]
  public static void Run()
  {
    LayoutAnchorTests.Run();
    NativeViewerGrowthTests.Run();
    LayoutResizeTests.Run();
  }
}
