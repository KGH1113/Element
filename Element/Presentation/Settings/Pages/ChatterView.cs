using Element.Domain.Tweaks;
using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.Pages;

internal sealed class ChatterView : View<ChatterModel>
{
  public ChatterView(ChatterModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
      spacing: 12,
      SwitchCard(
        L("채터 블로커", "Chatter blocker"),
        L("튄 키 입력을 한 번으로 셉니다.", "Counts a bouncing key press only once."),
        Bind(() => VM.Settings.Gameplay.Chatter.Enabled)
      ),
      Threshold(),
      Text(
          L(
            "같은 키를 마지막으로 받은 입력에서 이 시간 안에 다시 누르면 무시해요. 이보다 빠르게 같은 키를 연타하면 입력이 빠지니, 그런 키는 아래에 넣어 주세요.",
            "A press of the same key within this time of its last accepted press is ignored. Faster repeats on one key are dropped too, so add such keys below."
          )
        )
        .Font(Font.Caption)
        .Foreground(Colors.Muted),
      IgnoredKeys(),
      Text(() => VM.Blocked.Value, format: L("이번 실행에서 막은 입력: ", "Blocked since ADOFAI started: ") + "{0}")
        .Font(Font.Caption)
        .Foreground(Colors.Muted),
      If(
        () => VM.OtherBlockerRunning,
        () =>
          Text(
              L(
                "KeyboardChatterBlocker도 켜져 있어요. 입력이 두 번 걸러지지 않도록 한쪽만 사용하세요.",
                "KeyboardChatterBlocker is also running. Use only one of them so input is not filtered twice."
              )
            )
            .Font(Font.Caption)
            .Foreground(Colors.Danger)
      )
    );

  /// <summary>The threshold: dragged, or typed as a whole number of milliseconds. It waits while blocking is off.</summary>
  private View Threshold() =>
    VStack(
        spacing: 8,
        Text(L("채터 임계값", "Chatter threshold")).Font(Font.Caption).Foreground(Colors.Muted),
        // The typed field sits beside the slider, or under it where the slider would be too short.
        ViewThatFits(
          HStack(spacing: 12, ThresholdSlider(), ThresholdField())
            .Frame(minWidth: 440, maxWidth: float.PositiveInfinity),
          VStack(spacing: 8, ThresholdSlider(), ThresholdField())
        )
      )
      .Padding(16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12)
      .Disabled(() => !VM.Settings.Gameplay.Chatter.Enabled);

  private View ThresholdSlider() =>
    Slider(null, Bind(() => VM.Threshold), ChatterFilter.MinimumWindowMs, ChatterFilter.MaximumWindowMs, step: 1, showsValue: false);

  private View ThresholdField() =>
    HStack(
      spacing: 12,
      NumberField(null, Bind(() => VM.Threshold), ChatterFilter.MinimumWindowMs, ChatterFilter.MaximumWindowMs)
        .Frame(width: 72),
      Text("ms").Font(Font.Caption).Foreground(Colors.Muted)
    );

  /// <summary>Keys the blocker leaves alone, such as one used for fast jacks: Change keys listens, each key goes on or comes off.</summary>
  private View IgnoredKeys() =>
    VStack(
        spacing: 8,
        Text(L("막지 않을 키", "Keys never blocked")).Font(Font.Heading),
        Text(() =>
            VM.Capturing
              ? L(
                "누르는 키를 넣거나 뺍니다. 끝나면 완료를 누르거나 Esc를 누르세요.",
                "Each key you press is added, or removed if it's already here. Press Done or Esc when finished."
              )
              : L(
                "여기 넣은 키는 아무리 빠르게 연타해도 막지 않아요.",
                "Keys here are never blocked, however fast you repeat them."
              )
          )
          .Font(Font.Caption)
          .Foreground(Colors.Muted),
        Text(() => VM.IgnoredKeysLabel)
          .Font(Font.Callout)
          .Foreground(() => VM.HasIgnoredKeys ? Colors.Foreground : Colors.Muted),
        HStack(
            spacing: 8,
            Button(
              Text(() => VM.Capturing ? L("완료", "Done") : L("키 바꾸기", "Change keys")),
              () => VM.ChangeKeys,
              ButtonStyle.Prominent
            ),
            Button(Text(L("비우기", "Clear")), () => VM.ClearKeys)
          )
          .Padding(new Insets(4, 0, 0, 0))
      )
      .Padding(16)
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
      .Background(Colors.Surface, 12);
}
