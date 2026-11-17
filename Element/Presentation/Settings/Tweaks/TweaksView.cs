using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Tweaks;

internal sealed class TweaksView : View<TweaksViewModel>
{
  public TweaksView(TweaksViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      Link(
        L("에디터", "Editor tweaks"),
        L("선택 정보, 정밀 회전, 이벤트 좌표", "Selection info, fine rotation and event positions"),
        () => VM.OpenEditor
      ),
      Link(
        L("판정 표시", "Judgment visuals"),
        L("어떤 판정 문구를 보여줄지 선택합니다.", "Choose which judgment popups appear."),
        () => VM.OpenJudgment
      ),
      Link(
        L("행성", "Planet appearance"),
        L("공, 꼬리, 고리의 색상과 불투명도", "Body, trail and ring color & opacity"),
        () => VM.OpenPlanets
      ),
      Link(
        L("채터 블로커", "Chatter blocker"),
        L(
          "스위치가 튀어 한 번 누른 키가 두 번 입력되는 것을 막습니다.",
          "Stops a bouncing switch from counting one press twice."
        ),
        () => VM.OpenChatter
      ),
      // Side by side on a wide window: the switches share a row with room for both.
      Grid(minColumnWidth: 374, spacing: 12, Switches())
    );

  private View[] Switches()
  {
    View record = SwitchCard(
      L("녹화 모드", "Record mode"),
      L("게임 HUD와 모드 오버레이를 숨깁니다.", "Hide the game HUD and mod overlays."),
      Bind(() => VM.Settings.RecordMode)
    );
    if (!VM.HasTrackpad)
      return new[] { record };
    return new[]
    {
      record,
      SwitchCard(
        L("macOS 트랙패드", "macOS Trackpad"),
        L(
          "에디터에서 두 손가락으로 이동하고 핀치로 확대 · 축소합니다.",
          "In the editor, pan with two fingers and pinch to zoom."
        ),
        Bind(() => VM.Settings.NativeEditorTrackpad)
      ),
    };
  }

  /// <summary>A card that opens a page: its name, what it holds, and an arrow.</summary>
  private static View Link(string title, string detail, Expression<Func<Command>> open) =>
    Button(
      HStack(
          spacing: 16,
          VStack(spacing: 4, Text(title).Font(Font.Heading), Text(detail).Font(Font.Caption).Foreground(Colors.Muted))
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
          Image(ElementIcons.Of(UiIconKind.PageForward), 20).Foreground(Colors.Accent)
        )
        .Frame(maxWidth: float.PositiveInfinity, minHeight: 40, alignment: Alignment.Leading),
      open,
      ButtonStyle.Card
    );
}
