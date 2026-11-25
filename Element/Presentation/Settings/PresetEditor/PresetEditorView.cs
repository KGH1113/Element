using Element.Configuration.Effects;
using Element.Presentation.Settings.Effects;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.PresetEditor;

/// <summary>
/// The preset editor: name, Save and Delete; the tabs; then a tab's effect
/// switches (Screen leads with camera movement and its zoom) or the shortcut.
/// Narrow windows put Save under the name, the tabs in two rows and the
/// switches in one column.
/// </summary>
internal sealed class PresetEditorView : View<PresetEditorViewModel>
{
  // The typed zoom's width.
  private const float ZoomField = 84;

  public PresetEditorView(PresetEditorViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      ViewThatFits(Identity(true).Frame(minWidth: 600, maxWidth: float.PositiveInfinity), Identity(false)),
      If(
        () => VM.NameProblem.Value.Length > 0,
        () => Text(() => VM.NameProblem.Value).Font(Font.Caption).Foreground(Colors.Danger),
        () =>
          Text(L("이 프리셋에서 제거할 이펙트를 선택하세요.", "Choose the effects this preset removes."))
            .Font(Font.Caption)
            .Foreground(Colors.Muted)
      ),
      ViewThatFits(TabBar(6).Frame(minWidth: 600, maxWidth: float.PositiveInfinity), TabBar(3)),
      // Each tab is made on first use and retains its view state while inactive.
      UI.Switch(
        () => VM.Tab,
        tab =>
          tab switch
          {
            (int)EffectGroup.Screen => ScreenTab(),
            PresetEditorViewModel.ShortcutTab => ShortcutTab(),
            _ => GroupTab((EffectGroup)tab),
          }
      )
    );

  // The name, Save and, for a saved preset, Delete; on narrow windows Save and Delete go under the name.
  private View Identity(bool wide)
  {
    View name = TextField(Bind(() => VM.Name), L("프리셋 이름", "Preset name"))
      .CharacterLimit(EffectSettings.MaximumNameLength)
      .FocusOn(() => VM.FocusName)
      .Frame(maxWidth: float.PositiveInfinity);
    View save = Button(
        Text(L("저장", "Save preset")).Frame(maxWidth: float.PositiveInfinity),
        () => VM.Save,
        ButtonStyle.Prominent
      )
      .Frame(width: 152);
    // Only a saved preset can be deleted; the space before it goes with it.
    View delete = If(
      () => !VM.IsNew,
      () =>
        Button(Image(ElementIcons.Of(UiIconKind.Delete), 20), () => VM.Delete, ButtonStyle.Standard)
          .Frame(width: 40, height: 40)
          .Padding(new Insets(0, wide ? 8 : 0, 0, wide ? 0 : 8))
    );
    return wide
      ? HStack(spacing: 0, name, save.Padding(new Insets(0, 8, 0, 0)), delete)
      : VStack(spacing: 12, name, HStack(spacing: 0, delete, Spacer(), save).Frame(maxWidth: float.PositiveInfinity));
  }

  // The effect groups and Shortcut, the shown one filled like a segmented control's pick.
  private View TabBar(int columns)
  {
    View[] tabs = new View[VM.TabNames.Length];
    for (int i = 0; i < tabs.Length; i++)
      tabs[i] = TabButton(i);
    return Grid(columns, 4, tabs)
      .Padding(4)
      .Frame(maxWidth: float.PositiveInfinity)
      .Background(Colors.Surface, 22, Colors.Line);
  }

  private View TabButton(int index) =>
    Button(
        Text(VM.TabNames[index])
          .Font(Font.Callout)
          .Foreground(() => VM.Tab == index ? Colors.OnAccent : Colors.Foreground)
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        () => VM.SelectTab(index),
        ButtonStyle.Plain
      )
      .Frame(height: 34)
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.Tab == index ? Colors.Accent : Colors.Surface, 17));

  // Camera movement first, its zoom under it, then the rest of the Screen group.
  private View ScreenTab() =>
    VStack(spacing: 12, EffectCard(VM.Camera), Zoom(), Effects(VM.Groups[(int)EffectGroup.Screen]));

  private View GroupTab(EffectGroup group) => Effects(VM.Groups[(int)group]);

  // Two columns where two fit; 305 keeps the widest window (936) at two.
  private static View Effects(EffectChoiceViewModel[] choices) =>
    Grid(minColumnWidth: 305, spacing: 12, ForEach(choices, choice => choice.Definition.Flag, EffectCard));

  private static View EffectCard(ItemSlot<EffectChoiceViewModel> slot) =>
    SwitchCard(() => slot.Value.Title, () => slot.Value.Description, Bind(() => slot.Value.Removed));

  private static View EffectCard(EffectChoiceViewModel choice) =>
    SwitchCard(choice.Title, choice.Description, Bind(() => choice.Removed));

  // The zoom follows the planets once camera movement is removed; it is off until then.
  private View Zoom() =>
    VStack(
        spacing: 8,
        Text(L("카메라 확대", "Camera zoom")).Font(Font.Caption).Foreground(Colors.Muted),
        // The typed field shows the number, so the slider leaves out its own.
        HStack(
          spacing: 12,
          Slider(
            null,
            Bind(() => VM.Zoom),
            EffectPreset.MinimumCameraZoom,
            EffectPreset.MaximumCameraZoom,
            step: 10,
            showsValue: false
          ),
          HStack(
            spacing: 8,
            NumberField(null, Bind(() => VM.Zoom), EffectPreset.MinimumCameraZoom, EffectPreset.MaximumCameraZoom)
              .Frame(width: ZoomField),
            Text("%").Font(Font.Caption).Foreground(Colors.Muted).Frame(width: 16)
          )
        )
      )
      .Padding(14, 16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12)
      .Disabled(() => !VM.Camera.Removed);

  private View ShortcutTab() =>
    VStack(
        spacing: 8,
        Text(L("단축키", "Shortcut")).Font(Font.Heading.Sized(17)),
        Text(
            L(
              "누르면 이 프리셋을 적용하고, 한 번 더 누르면 이펙트 제거를 끕니다. 플레이 중에는 동작하지 않습니다.",
              "Press it to apply this preset, and again to turn effect removal off. It does nothing during play."
            )
          )
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        new ShortcutField(VM.Shortcut, UiIconKind.Close).Padding(new Insets(8, 0, 0, 0))
      )
      .Padding(16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12);
}
