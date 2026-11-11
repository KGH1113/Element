using Element.Application.Rendering;
using Element.Presentation.Rendering;
using static Element.Tests.Application.Rendering.RenderFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Rendering;

/// <summary>Why a render has no key viewers, and what the player is told to do.</summary>
internal static class RenderFailureTests
{
  internal static void Run()
  {
    NeedsARendererThatTakesVideos();
    ExplainsWhyKeyViewersAreMissing();
  }

  private static void NeedsARendererThatTakesVideos()
  {
    var ready = new FakeTarget();
    Check(KeyViewerRenderPlan.Blocker(ready, true, true) == null, "Everything is in place.");
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { TakesVideos = false }, true, true)?.Code
        == RenderFailure.RendererOutdated,
      "OrbitRender that can't lay videos over the render needs an update."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { Ffmpeg = null }, true, true)?.Code == RenderFailure.RendererOutdated,
      "So does a TUFReplay that shares no FFmpeg."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { Fps = 0 }, true, true)?.Code == RenderFailure.RendererOutdated,
      "Or reports no frame rate."
    );
    Check(
      KeyViewerRenderPlan.Blocker(new FakeTarget { TakesVideos = false }, false, false)?.Code
        == RenderFailure.RendererOutdated,
      "The renderer comes first: opening ElementNote wouldn't help."
    );
    Check(
      KeyViewerRenderPlan.Blocker(ready, false, true)?.Code == RenderFailure.AppOutdated,
      "A connected ElementNote without protocol 2 needs an update."
    );
    Check(
      KeyViewerRenderPlan.Blocker(ready, false, false)?.Code == RenderFailure.AppUnavailable,
      "No ElementNote at all needs opening."
    );
  }

  private static void ExplainsWhyKeyViewersAreMissing()
  {
    (string title, string detail) = RenderNotices.For(RenderFailure.RendererOutdated, true);
    Check(title == "키뷰어 없이 렌더링했어요", "The title says what happened.");
    Check(detail == "TUFReplay와 OrbitRender를 업데이트한 뒤 다시 렌더링해주세요.", "The detail says what to do.");
    Check(
      RenderNotices.For(RenderFailure.RendererOutdated, false).Detail
        == "Update TUFReplay and OrbitRender, then render again.",
      "And in English."
    );
    Check(
      RenderNotices.For(RenderFailure.AppOutdated, false).Detail == "Update ElementNote, then render again.",
      "An old ElementNote is updated."
    );
    Check(
      RenderNotices.For(RenderFailure.AppUnavailable, false).Detail.StartsWith("Open ElementNote"),
      "A closed one is opened."
    );
    Check(
      RenderNotices
        .For("render-ffmpeg-failed", false)
        .Detail.StartsWith("ElementNote couldn't make the key viewer video"),
      "An encoding failure says the video wasn't made."
    );
  }
}
