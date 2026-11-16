using Element.Presentation.Ui.Theme;

namespace Element.Presentation.Notices;

/// <summary>What ADOFAI's Key Limiter row in the pause settings says while ElementNote's keys apply.</summary>
internal static class KeyLimiterMenuText
{
  public static string Label => UiLocale.L("ElementNote 키", "ElementNote keys");

  public static string Description =>
    UiLocale.L(
      "ElementNote가 연결되어 있는 동안에는 ElementNote의 키만 입력돼요. 직접 고른 키를 쓰려면 ElementNote 설정의 '보여 줄 곳'에서 키 리미터를 꺼 주세요.",
      "While ElementNote is connected, only its keys are accepted. Turn off Key limiter under Settings > Where to show in ElementNote to use your own keys."
    );
}
