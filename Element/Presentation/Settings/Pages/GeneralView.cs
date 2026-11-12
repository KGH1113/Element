using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

/// <summary>General: the profiles link first, the settings shortcut, then a switch per preference.</summary>
internal sealed class GeneralView : View<GeneralModel>
{
  public GeneralView(GeneralModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      // Rows left out of this install are left out of the stack too.
      VM.HasProfiles
        ? ProfilesLink()
        : null,
      VStack(
          spacing: 16,
          Text(L("설정 단축키", "Settings shortcut")).Font(Font.Heading.Sized(17)),
          new ShortcutField(VM.Shortcut, UiIconKind.Reset)
        )
        .Padding(16)
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
        .Background(Colors.Surface, 12),
      SwitchCard(
        L("움직임 줄이기", "Reduce motion"),
        L("설정 전환과 스위치 움직임을 즉시 적용합니다.", "Use immediate menu and switch transitions."),
        Bind(() => VM.Settings.ReduceUiMotion)
      ),
      SwitchCard(
        L("ElementNote 자동 열기/닫기", "Auto open/close ElementNote"),
        L(
          "게임을 켜면 ElementNote를 열고, 게임을 끄면 함께 닫습니다.",
          "Open ElementNote with the game and close it when the game quits."
        ),
        Bind(() => VM.Settings.AutoOpenCloseDmNote)
      ),
      VM.HasUpdates
        ? SwitchCard(
          L("자동 업데이트", "Automatic updates"),
          () => VM.UpdateStatus.Value,
          Bind(() => VM.Updates.AutomaticUpdates)
        )
        : null
    );

  // Profiles, naming the one in use; it opens the Profiles page.
  private View ProfilesLink() =>
    Button(
        HStack(
            spacing: 16,
            VStack(
                spacing: 6,
                Text(L("프로필", "Profiles")).Font(Font.Heading.Sized(17)),
                Text(() => VM.Window.Profiles.InUse)
                  .Font(Font.Caption)
                  .Foreground(Colors.Muted)
                  .Configure<TMPro.TMP_Text>(text => text.overflowMode = TMPro.TextOverflowModes.Ellipsis)
                  .Frame(maxHeight: 20, alignment: Alignment.Leading)
              )
              .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
            Image(ElementIcons.Of(UiIconKind.PageForward), 22).Foreground(Colors.Accent)
          )
          .Frame(maxWidth: float.PositiveInfinity)
          .Padding(new Insets(0, 8, 0, 8)),
        () => VM.OpenProfiles,
        ButtonStyle.Row
      )
      .Frame(minHeight: 80)
      .Background(Colors.Surface, 12);
}
