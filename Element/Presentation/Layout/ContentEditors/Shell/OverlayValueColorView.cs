using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// A color that follows a value: which value it follows, the band with its
/// stops, the picked stop's value, color and removal, and whether the color
/// blends or jumps between stops.
/// </summary>
internal sealed class OverlayValueColorView : View<OverlayValueColorModel>
{
  public const float RowHeight = 36,
    StopRowHeight = 32,
    ModeHeight = 32,
    ModeWidth = 196,
    Gap = 10;

  public OverlayValueColorView(OverlayValueColorModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(spacing: Gap, SourceRow(), new OverlayValueColorBandView(VM), StopRow(), ModeRow());

  // How the color changes between stops, named on the left.
  private View ModeRow() =>
    HStack(
      spacing: 8,
      UI.Text(L("바뀌는 방식", "Changes"))
        .Font(Font.Caption.Sized(13))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Padding(new Insets(0, 4, 0, 0)),
      Spacer(minLength: 0),
      Picker(null, new[] { L("부드럽게", "Smoothly"), L("바로", "At once") }, Bind(() => VM.Mode))
        .Frame(width: ModeWidth, height: ModeHeight)
    );

  // The value the color follows; a click offers the ones to pick from.
  private View SourceRow() =>
    Button(
        HStack(
            spacing: 0,
            UI.Text(L("기준 값", "Follows")).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1),
            Spacer(minLength: 8),
            UI.Text(() => VM.SourceName.Value)
              .Font(Font.Caption.Sized(13))
              .Foreground(() => VM.SourceInk.Value)
              .LineLimit(1),
            Image(ElementIcons.Of(UiIconKind.Down), 12).Foreground(Colors.Muted).Padding(new Insets(0, 6, 0, 0))
          )
          .Padding(new Insets(0, 12, 0, 12))
          .Frame(maxWidth: float.PositiveInfinity, height: RowHeight),
        () => VM.PickSource,
        ButtonStyle.Plain
      )
      .Background(() => VM.SourceFill.Value, 10)
      .Named("Value color source");

  // The picked stop: its value (typed in the dock), its color (in the
  // picker) and removing it.
  private View StopRow() =>
    HStack(
      spacing: 6,
      Button(
          HStack(
              spacing: 0,
              UI.Text(L("값", "At")).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1),
              Spacer(minLength: 6),
              UI.Text(() => VM.StopValue.Value).Font(Font.Caption.Sized(14)).LineLimit(1)
            )
            .Padding(new Insets(0, 10, 0, 10))
            .Frame(width: 104, height: StopRowHeight),
          () => VM.EditValue,
          ButtonStyle.Plain
        )
        .Background(() => VM.StopValueFill.Value, 8)
        .Named("Stop value"),
      Button(
          HStack(
              spacing: 0,
              new SwatchDot(() => VM.StopColor.Value, () => VM.NoGradient.Value),
              UI.Text(() => VM.StopColorName.Value)
                .Font(Font.Caption.Sized(13))
                .LineLimit(1)
                .Padding(new Insets(0, 8, 0, 0)),
              Spacer(minLength: 4),
              Image(ElementIcons.Of(UiIconKind.Forward), 12).Foreground(Colors.Muted)
            )
            .Padding(new Insets(0, 8, 0, 8))
            .Frame(maxWidth: float.PositiveInfinity, height: StopRowHeight),
          () => VM.PickColor,
          ButtonStyle.Plain
        )
        .Background(() => VM.StopColorFill.Value, 8)
        .Named("Stop color"),
      Button(
          Image(ElementIcons.Of(UiIconKind.RemoveStop), 16)
            .Foreground(() => VM.RemoveInk.Value)
            .Frame(width: StopRowHeight, height: StopRowHeight),
          () => VM.Remove,
          ButtonStyle.Plain
        )
        .Background(VM.Surface, 8)
        .Named("Remove stop")
    );
}
