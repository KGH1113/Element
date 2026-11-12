using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static ElementUI.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

internal sealed class HomeView : View<HomeModel>
{
  public HomeView(HomeModel vm)
    : base(vm) { }

  public override View Body =>
    Grid(
      columns: 2,
      spacing: 16,
      Category(
        UiIconKind.Overlay,
        "Overlay",
        L("요소를 추가하고 색상과 배치를 편집합니다.", "Add elements and adjust their color and layout."),
        L("오버레이 설정", "Edit overlays"),
        () => VM.EditOverlay
      ),
      Category(
        UiIconKind.Tweaks,
        "Tweaks",
        L("판정 표시와 게임플레이 동작을 설정합니다.", "Adjust judgment display and gameplay behavior."),
        L("게임플레이 설정", "Open tweaks"),
        () => VM.OpenTweaks
      ),
      Category(
        UiIconKind.Effects,
        "Effects",
        L("필요한 이펙트만 남기는 나만의 프리셋.", "Keep the effects you want with personal presets."),
        L("프리셋 선택", "Choose a preset"),
        () => VM.OpenEffects
      ),
      Category(
        UiIconKind.General,
        L("일반", "General"),
        VM.HasProfiles
          ? L(
            "프로필, 설정 단축키, UI 움직임, ElementNote 자동 실행, 업데이트를 설정합니다.",
            "Profiles, your shortcut, motion, ElementNote and update preferences."
          )
          : L(
            "설정 단축키, UI 움직임, ElementNote 자동 실행, 업데이트를 설정합니다.",
            "Set your shortcut, motion, ElementNote and update preferences."
          ),
        L("일반 설정", "Open general"),
        () => VM.OpenGeneral
      )
    );

  /// <summary>A category card: icon and title, what it holds, and what opening it does.</summary>
  private static View Category(
    UiIconKind icon,
    string title,
    string description,
    string action,
    Expression<Func<Command>> open
  ) =>
    Button(
        VStack(
            spacing: 14,
            HStack(spacing: 16, Image(ElementIcons.Of(icon), 32).Foreground(Colors.Muted), Text(title).Font(Font.Title.Sized(25))),
            Text(description).Foreground(Colors.Muted).Font(Font.Callout),
            Spacer(),
            Divider(),
            HStack(Text(action).Font(Font.Callout), Spacer(), Image(ElementIcons.Of(UiIconKind.PageForward), 20))
              .Foreground(Colors.Accent)
          )
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity, alignment: Alignment.TopLeading),
        open,
        ButtonStyle.Card
      )
      .Frame(height: 208);
}
