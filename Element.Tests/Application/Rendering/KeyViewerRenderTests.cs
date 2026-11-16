using Element.Tests.Domain.Rendering;
using Element.Tests.Support;

namespace Element.Tests.Application.Rendering;

/// <summary>
/// Replay renders with key viewers, one file per part: the render session,
/// ElementNote's replies, where viewers go in the video, the overlay of each page,
/// and why a render has no key viewers.
/// </summary>
internal static class KeyViewerRenderTests
{
  [Suite("replay renders send key viewer frames without waiting and lay ElementNote's videos in place")]
  public static void Run()
  {
    KeyViewerRenderSessionTests.Run();
    RenderReplyTests.Run();
    RenderPlacementTests.Run();
    KeyViewerOverlayTests.Run();
    RenderFailureTests.Run();
  }
}
