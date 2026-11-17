using Element.Presentation.Settings.Chatter;
using Element.Presentation.Settings.EditorTweaks;
using Element.Presentation.Settings.Effects;
using Element.Presentation.Settings.General;
using Element.Presentation.Settings.Home;
using Element.Presentation.Settings.Judgment;
using Element.Presentation.Settings.Planets;
using Element.Presentation.Settings.PresetEditor;
using Element.Presentation.Settings.Profiles;
using Element.Presentation.Settings.Tweaks;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Window;

/// <summary>
/// The window's frame: the title row with Back and Close, the page shown,
/// and the save message. Each page is made the first time it shows and kept.
/// The frame keeps 32 at its sides, 20 on a window under 600 wide.
/// </summary>
internal sealed class SettingsWindowView : View<SettingsWindowViewModel>
{
  public SettingsWindowView(SettingsWindowViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 0,
      Sides(
          HStack(
              spacing: 12,
              If(
                () => VM.CanGoBack.Value,
                () => IconButton(UiIconKind.PageBack, () => VM.Back).Hint(L("뒤로", "Back"))
              ),
              Text(() => VM.Current.Title).Font(Font.Title).LineLimit(1),
              Spacer(),
              IconButton(UiIconKind.WindowClose, () => VM.Close).Hint(L("닫기", "Close"))
            )
            .Frame(height: 48)
        )
        .Padding(new Insets(24, 0, 24, 0)),
      ZStack(
          Alignment.TopLeading,
          Page(VM.Home, () => new HomeView(VM.Home)),
          Page(VM.Tweaks, () => new TweaksView(VM.Tweaks)),
          Page(VM.EditorTweaks, () => new EditorTweaksView(VM.EditorTweaks)),
          Page(VM.Judgment, () => new JudgmentView(VM.Judgment)),
          Page(VM.Planets, () => new PlanetsView(VM.Planets)),
          Page(VM.Chatter, () => new ChatterView(VM.Chatter)),
          Page(VM.Effects, () => new EffectsView(VM.Effects)),
          Page(VM.PresetEditor, () => new PresetEditorView(VM.PresetEditor)),
          Page(VM.General, () => new GeneralView(VM.General)),
          VM.Profiles != null ? Page(VM.Profiles, () => new ProfilesView(VM.Profiles)) : Spacer()
        )
        .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity, alignment: Alignment.TopLeading),
      If(
        () => VM.SaveFailed.Value,
        () =>
          Sides(
              HStack(
                spacing: 16,
                Text(() => VM.SaveError).Foreground(Colors.Danger).Font(Font.Caption),
                Spacer(),
                Button(Text(L("다시 저장", "Retry save")), () => VM.RetrySave)
              )
            )
            .Padding(new Insets(12, 0, 16, 0))
      )
    );

  /// <summary>A page that shows while it is the current one, scrolled on its own.</summary>
  private View Page(SettingsPageViewModel page, System.Func<View> content) =>
    If(
      () => VM.Current == page,
      () =>
        ScrollView(Sides(content()).Padding(new Insets(0, 0, 24, 0)))
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity)
    );

  /// <summary>The window's side space follows its available width.</summary>
  private View Sides(View content) =>
    content
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .Padding(() => new Insets(0, VM.Compact ? 20 : 32));

  private static View IconButton(UiIconKind icon, System.Linq.Expressions.Expression<System.Func<Command>> command) =>
    Button(Image(ElementIcons.Of(icon), 28), command, ButtonStyle.Icon);
}
