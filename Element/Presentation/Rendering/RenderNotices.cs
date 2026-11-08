using Element.Application.Rendering;

namespace Element.Presentation.Rendering;

/// <summary>
/// The notice after a render whose video has no key viewers: what happened, then what
/// to do. Shown after the render, so it never appears in the video.
/// </summary>
internal static class RenderNotices
{
  public static (string Title, string Detail) For(string code, bool korean) =>
    (L(korean, "키뷰어 없이 렌더링했어요", "Rendered without the key viewers"), Detail(code, korean));

  private static string Detail(string code, bool korean) =>
    code switch
    {
      RenderFailure.AppUnavailable => L(
        korean,
        "ElementNote를 켠 뒤 다시 렌더링하면 키뷰어가 들어가요.",
        "Open ElementNote, then render again to include them."
      ),
      RenderFailure.AppOutdated or RenderFailure.Incompatible => L(
        korean,
        "ElementNote를 최신 버전으로 업데이트한 뒤 다시 렌더링해주세요.",
        "Update ElementNote, then render again."
      ),
      RenderFailure.RendererOutdated => L(
        korean,
        "TUFReplay와 OrbitRender를 업데이트한 뒤 다시 렌더링해주세요.",
        "Update TUFReplay and OrbitRender, then render again."
      ),
      "render-busy" => L(
        korean,
        "다른 렌더가 ElementNote를 쓰고 있었어요. 그 렌더가 끝난 뒤 다시 렌더링해주세요.",
        "Another render was using ElementNote. Render again after it finishes."
      ),
      "render-platform-unsupported" => L(
        korean,
        "이 운영체제에서는 아직 키뷰어를 영상에 넣을 수 없어요.",
        "This system can't put key viewers into videos yet."
      ),
      RenderFailure.Timeout => L(
        korean,
        "ElementNote가 응답하지 않았어요. ElementNote를 다시 연 뒤 렌더링해주세요.",
        "ElementNote stopped answering. Reopen ElementNote, then render again."
      ),
      _ => L(
        korean,
        "ElementNote가 키뷰어 영상을 만들지 못했어요. ElementNote를 다시 연 뒤 렌더링해주세요.",
        "ElementNote couldn't make the key viewer video. Reopen ElementNote, then render again."
      ),
    };

  private static string L(bool korean, string ko, string en) => korean ? ko : en;
}
