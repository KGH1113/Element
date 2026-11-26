using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The parts every color picker layout is made of: the square, the hue and
/// opacity bars, Highlight/Custom, the HEX field with the color's swatch,
/// the opacity box, Default and the line that says what can't be read.
/// <see cref="ColorPickerCardView"/> and <see cref="ColorPickerColumnsView"/>
/// put them together.
/// </summary>
internal abstract class ColorPickerLayout : View<ColorPickerViewModel>
{
  public const float Column = 256,
    FieldHeight = 40,
    Box = 72;

  /// <param name="parts">What the owner keeps (its opacity box, the color bar); null where there are none.</param>
  protected ColorPickerLayout(ColorPickerViewModel vm, ColorPickerParts parts)
    : base(vm) => Parts = parts;

  protected ColorPickerParts Parts { get; }

  protected View Plane(float height = ColorPlaneView.Height) => new ColorPlaneView(VM, height);

  protected View Hue() => new ColorSliderView(VM, true);

  protected View Opacity() => new ColorSliderView(VM, false);

  /// <summary>Highlight/Custom: follow the highlight color, or a color of its own.</summary>
  protected View Source() =>
    If(
      () => VM.SourceShown.Value,
      () =>
        Picker(null, new[] { L("하이라이트", "Highlight"), L("직접 고르기", "Custom") }, Bind(() => VM.Source))
          .Font(Font.Caption.Sized(13))
          .Frame(height: 36)
    );

  /// <summary>HEX with the color's swatch, then the opacity, Default, or nothing.</summary>
  protected View HexRow() =>
    HStack(
        spacing: 8,
        HexField(),
        If(() => VM.OpacityShown.Value, OpacityBox, () => If(() => VM.ResetShown.Value, DefaultButton))
      )
      .Frame(height: FieldHeight);

  /// <summary>The opacity's name with its box, while following the highlight leaves only the opacity.</summary>
  protected View OpacityRow() =>
    HStack(
        spacing: 8,
        UI.Text(() => VM.OpacityLabel.Value)
          .Font(Font.Caption)
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        OpacityBox()
      )
      .Frame(height: FieldHeight);

  /// <summary>Why the typed color can't be used, in the space it keeps.</summary>
  protected View Error(float height) =>
    UI.Text(() => VM.Error.Value)
      .Font(Font.Caption.Sized(11))
      .Foreground(Colors.Danger)
      .LineLimit(1)
      .Named("Color picker error")
      .Frame(maxWidth: float.PositiveInfinity, height: height, alignment: Alignment.Leading);

  protected View HexField() =>
    FieldSurface(
      HStack(
          spacing: 10,
          Circle().Fill(() => Opaque(VM.Swatch.Value)).Frame(width: 16, height: 16),
          TextField(Bind(() => VM.Hex))
            .TextFieldStyle(TextFieldStyle.Plain)
            .CharacterLimit(7)
            .OnEditEnded((_, _) => VM.Commit())
            .FocusOn(() => VM.FocusHex.Value)
            .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity)
            .Named("HEX color")
        )
        .Padding(new Insets(4, 12, 4, 8))
    );

  /// <summary>The opacity's box across the room it is given.</summary>
  protected View WideOpacityBox() =>
    If(
        () => VM.OpacityOutside.Value,
        () => new HostedRect(Parts.OpacitySlot, Parts.Park, float.NaN, FieldHeight),
        () => TypedOpacity(L("불투명도", "Opacity"))
      )
      .Frame(maxWidth: float.PositiveInfinity, height: FieldHeight);

  // The owner's own box where it shows the opacity, or the typed one.
  private View OpacityBox() =>
    If(
        () => VM.OpacityOutside.Value,
        () => new HostedRect(Parts.OpacitySlot, Parts.Park, Box, FieldHeight),
        () => TypedOpacity()
      )
      .Frame(width: Box, height: FieldHeight);

  // Named where the box is wide enough to say what it holds.
  private View TypedOpacity(string name = null) =>
    FieldSurface(
      HStack(
          spacing: 1,
          name == null ? null : UI.Text(name).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1),
          TextField(Bind(() => VM.Opacity))
            .TextFieldStyle(TextFieldStyle.Plain)
            .InputKind(TextInputKind.Integer)
            .CharacterLimit(3)
            .TextAlignment(TextAlign.Trailing)
            .OnEditEnded((_, _) => VM.Commit())
            .FocusOn(() => VM.FocusOpacity.Value)
            .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity)
            .Named("Opacity percentage"),
          UI.Text("%").Font(Font.Caption.Sized(13)).Foreground(Colors.Muted)
        )
        .Padding(new Insets(4, 10, 4, 12))
    );

  private static View FieldSurface(View content) =>
    EnvironmentReader(environment =>
      content.Background(Colors.SurfaceRaised, environment.Theme.CornerRadius, Colors.Line)
    );

  private View DefaultButton() =>
    Button(
        UI.Text(L("기본값", "Default"))
          .Font(Font.Caption)
          .LineLimit(1)
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        () => VM.Reset,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(UiPalette.Surface, 8))
      .Named("Default color")
      .Frame(width: Box, height: FieldHeight);

  // The square, hue and opacity, then HEX with the opacity along the bottom.
  protected View OwnColumn() =>
    VStack(
      spacing: 0,
      Plane(),
      Hue().Padding(new Insets(8, 0, 0, 0)),
      If(() => VM.OpacityShown.Value, Opacity),
      Error(14).Padding(new Insets(4, 0, 0, 0)),
      HexRow().Padding(new Insets(4, 0, 0, 0))
    );

  // Following the highlight leaves only its opacity: a gradient's names the picked color.
  protected View HighlightColumn() =>
    VStack(
      spacing: 0,
      OpacityRow(),
      Opacity().Padding(new Insets(10, 0, 0, 0)),
      Error(14).Padding(new Insets(2, 0, 0, 0))
    );

  /// <summary>The close button of a picker with a title.</summary>
  protected View CloseButton() =>
    Button(Image(ElementIcons.Of(UiIconKind.WindowClose), 24), () => VM.Close, ButtonStyle.Icon)
      .Named("Close color picker")
      .Frame(width: 40, height: 40);

  [Pure]
  private static UiColor Opaque(Color color) => new Color(color.r, color.g, color.b, 1);
}
