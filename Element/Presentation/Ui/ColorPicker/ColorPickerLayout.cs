using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
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
internal abstract class ColorPickerLayout : View<ColorPickerModel>
{
  public const float Column = 256,
    FieldHeight = 40,
    Box = 72;

  /// <param name="parts">What the owner keeps (its opacity box, the color bar); null where there are none.</param>
  protected ColorPickerLayout(ColorPickerModel vm, ColorPickerParts parts)
    : base(vm) => Parts = parts;

  protected ColorPickerParts Parts { get; }

  protected View Plane() => new ColorPlaneView(VM);

  protected View Hue() => new ColorSliderView(VM, true);

  protected View Opacity() => new ColorSliderView(VM, false);

  /// <summary>Highlight/Custom: follow the highlight color, or a color of its own.</summary>
  protected View Source() =>
    Picker(null, new[] { L("하이라이트 색", "Highlight Color"), L("직접 고르기", "Custom Color") }, Bind(() => VM.Source))
      .Frame(height: 36);

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

  private View HexField() =>
    ZStack(
      Alignment.Leading,
      TextField(null, Bind(() => VM.Hex))
        .Configure<TMP_InputField>(field =>
        {
          field.characterLimit = 7;
          // Room for the swatch before the digits.
          RectPeer.Stretch(field.textViewport, 38, 4, 8, 4);
          // Middle, not midline: TMP draws the caret from the same line metrics as the text then.
          field.textComponent.alignment = TextAlignmentOptions.Left;
          field.onEndEdit.AddListener(_ => VM.Commit());
        })
        .Configure<TMP_InputField, int>(() => VM.FocusHex.Value, Focus)
        .Named("HEX color"),
      Spacer().Frame(width: 16, height: 16).Background(() => Opaque(VM.Swatch.Value), 8).Padding(new Insets(0, 12, 0, 0))
    );

  // The owner's own box where it shows the opacity, or the typed one.
  private View OpacityBox() =>
    If(
        () => VM.OpacityOutside.Value,
        () => new HostedRect(Parts.OpacitySlot, Parts.Park, Box, FieldHeight),
        TypedOpacity
      )
      .Frame(width: Box, height: FieldHeight);

  private View TypedOpacity() =>
    ZStack(
      Alignment.Trailing,
      TextField(null, Bind(() => VM.Opacity))
        .Configure<TMP_InputField>(field =>
        {
          field.characterLimit = 3;
          field.contentType = TMP_InputField.ContentType.IntegerNumber;
          RectPeer.Stretch(field.textViewport, 10, 4, 23, 4);
          field.textComponent.alignment = TextAlignmentOptions.Right;
          // Right-aligned text puts the caret on the field's edge; keep room for it.
          field.textComponent.margin = new Vector4(0, 0, 2, 0);
          field.onEndEdit.AddListener(_ => VM.Commit());
        })
        .Configure<TMP_InputField, int>(() => VM.FocusOpacity.Value, Focus)
        .Named("Opacity percentage"),
      UI.Text("%").Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).Padding(new Insets(0, 0, 0, 12))
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
      .Background(UiPalette.Surface, 8)
      .Named("Default color")
      .Frame(width: Box, height: FieldHeight);

  /// <summary>The close button of a picker with a title.</summary>
  protected View CloseButton() =>
    Button(Image(ElementIcons.Of(UiIconKind.WindowClose), 24), () => VM.Close, ButtonStyle.Icon)
      .Named("Close color picker")
      .Frame(width: 40, height: 40);

  // A field asked to take the keyboard does, once the picker refused what it holds.
  private static void Focus(TMP_InputField field, int request)
  {
    if (request <= 0 || !field.isActiveAndEnabled)
      return;
    field.Select();
    field.ActivateInputField();
  }

  [Pure]
  private static UiColor Opaque(Color color) => new Color(color.r, color.g, color.b, 1);
}
