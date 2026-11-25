using ElementUI;
using static Element.Presentation.Ui.Declarative.Controls.SwitchCards;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;

namespace Element.Presentation.Settings.EditorTweaks;

internal sealed class EditorTweaksView : View<EditorTweaksViewModel>
{
  public EditorTweaksView(EditorTweaksViewModel vm)
    : base(vm) { }

  public override View Body =>
    Tabs(Tab("selection", L("선택 정보", "Selection"), Selection), Tab("editing", L("편집 동작", "Editing"), Editing));

  /// <summary>What the selection info shows.</summary>
  private View Selection() =>
    VStack(
      spacing: 12,
      // Two columns where two fit; 305 keeps the widest window (936) at two.
      Grid(minColumnWidth: 305, spacing: 12, Shown()),
      SwitchCard(
        L("마지막 간격 제외", "Exclude last interval"),
        L(
          "각도·박자·시간 계산에서 마지막 타일 다음 간격을 뺍니다.",
          "Count angle, beats and time up to the last selected tile."
        ),
        Bind(() => VM.Settings.Gameplay.Editor.ExcludeLastInterval),
        () => VM.CanExcludeLastInterval.Value
      )
    );

  private View[] Shown() =>
    new[]
    {
      SwitchCard(
        L("타일 각도", "Tile angle"),
        L("선택한 타일의 각도 합계", "Total angle of selected tiles"),
        Bind(() => VM.Settings.Gameplay.Editor.ShowAngle)
      ),
      SwitchCard(
        L("박자 수", "Beat count"),
        L("선택 구간의 박자 합계", "Beats in your selection"),
        Bind(() => VM.Settings.Gameplay.Editor.ShowBeats)
      ),
      SwitchCard(
        L("타일 수", "Tile count"),
        L("선택한 타일의 개수", "Number of selected tiles"),
        Bind(() => VM.Settings.Gameplay.Editor.ShowCount)
      ),
      SwitchCard(
        L("구간 시간", "Duration"),
        L("선택 구간의 재생 시간", "Playback time of the selection"),
        Bind(() => VM.Settings.Gameplay.Editor.ShowDuration)
      ),
    };

  /// <summary>Fine rotation with its step, and moving event positions with the tiles.</summary>
  private View Editing() =>
    VStack(
      spacing: 12,
      SwitchCard(
        L("정밀 회전", "Fine rotation"),
        VM.FineRotationDetail,
        Bind(() => VM.Settings.Gameplay.Editor.FineRotation)
      ),
      RotationStep(),
      SwitchCard(
        L("이벤트 좌표 함께 변환", "Transform event positions"),
        L(
          "타일을 뒤집거나 회전할 때 카메라·타일 좌표도 맞춥니다.",
          "Keep camera and track event positions aligned when flipping or rotating tiles."
        ),
        Bind(() => VM.Settings.Gameplay.Editor.SyncEventPositions)
      )
    );

  /// <summary>The step angle: a few common ones, or one typed. It waits while fine rotation is off.</summary>
  private View RotationStep() =>
    VStack(
      spacing: 8,
      VStack(
          spacing: 8,
          Text(L("회전 각도", "Step angle")).Font(Font.Caption).Foreground(Colors.Muted),
          Flow(
            8,
            ForEach(
              VM.Presets,
              preset => preset.Label,
              preset =>
                Button(
                  Text(preset.Value.Label),
                  () => preset.Value.Use,
                  ButtonStyle.Standard,
                  () => preset.Value.Picked.Value
                )
            ),
            TextField(Bind(() => VM.RotationText))
              .CharacterLimit(6)
              // Enter or leaving the field keeps the angle; typing alone doesn't.
              .OnEditEnded((_, _) => VM.CommitRotation.TryExecute())
              .Frame(width: 92)
          )
        )
        .Padding(16)
        .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading)
        .Background(Colors.Surface, 12)
        .Disabled(() => !VM.Settings.Gameplay.Editor.FineRotation),
      // Only an error takes room: the cards keep their even gaps.
      If(
        () => VM.RotationError.Value.Length > 0,
        () =>
          Text(() => VM.RotationError.Value)
            .Font(Font.Caption)
            .Foreground(Colors.Danger)
            .Padding(new Insets(0, 16, 0, 16))
      )
    );
}
