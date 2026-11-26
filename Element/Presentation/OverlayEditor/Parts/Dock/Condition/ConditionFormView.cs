using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Condition;

/// <summary>
/// The condition's input method, in the keypad's place, as a fork. On top,
/// one bar holds what is compared: a value, the comparison in a round key,
/// a value. Lines run down from it to the two answers, which stand side by
/// side as tiles ("then", "otherwise"): a color shows as the color, a number
/// as the number, large. The answer the condition gives now is lit, with the
/// line that leads to it; the other waits, quieter. Each part is a click
/// into its slot.
/// </summary>
internal sealed class ConditionFormView : View<ConditionFormViewModel>
{
  private const float Width = KeypadView.Width,
    TileGap = 8,
    TileWidth = (Width - TileGap) / 2,
    // The fork: a stem down from the middle of the bar, out to the middle of each tile.
    Stem = Width / 2,
    LeftDrop = TileWidth / 2,
    RightDrop = Width - TileWidth / 2,
    Stroke = 2;

  // Its sizes follow the place it has: all of the input method's, or what a folded condition leaves under its strip.
  private readonly float _height,
    _question,
    _side,
    _sign,
    _signIcon,
    _fork,
    _bend,
    _tile,
    _swatch,
    _swatchRadius,
    _radius,
    _between;

  private readonly UiColor _fill = UiPalette.EditorControl;
  private readonly UiColor _live = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .17f);
  private readonly UiColor _accent = UiPalette.SoftAccent;
  private readonly UiColor _muted = UiPalette.Muted;
  private readonly UiColor _bad = UiPalette.Error;
  private readonly UiColor _noEdge = FieldLook.Clear;
  private readonly UiColor _hover = new Color(1, 1, 1, .05f);

  // The way not taken is still a way: plain to see on the dark, only without the highlight color.
  private readonly UiColor _path = new Color(1, 1, 1, .42f);
  private readonly UiColor _swatchEdge = new Color(1, 1, 1, .28f);

  /// <param name="height">How tall a place it has; lower than the input method's whole place, it is drawn smaller.</param>
  public ConditionFormView(ConditionFormViewModel vm, float height = KeypadView.MethodHeight)
    : base(vm)
  {
    bool small = height < KeypadView.MethodHeight;
    _height = height;
    _question = small ? 52 : 64;
    _side = small ? 38 : 48;
    _sign = small ? 36 : 44;
    _signIcon = small ? 21 : 26;
    _fork = small ? 18 : 22;
    _bend = small ? 7 : 9;
    _tile = height - _question - _fork;
    _swatch = small ? 36 : 46;
    _swatchRadius = small ? 11 : 14;
    _radius = small ? 14 : 16;
    _between = small ? 6 : 8;
  }

  public override View Body =>
    VStack(spacing: 0, Question(), Fork(), Answers())
      // "No" is a shake, not a sentence.
      .Offset(x: () => VM.Shake.X)
      .Frame(width: Width, height: _height, alignment: Alignment.Top);

  // What is compared: value, comparison, value, in one bar.
  private View Question() =>
    HStack(
        spacing: 4,
        Side(VM.Slots[OverlayPiece.Left]),
        Sign(VM.Slots[OverlayPiece.Compare]),
        Side(VM.Slots[OverlayPiece.Right])
      )
      .Padding(0, 8)
      .Frame(width: Width, height: _question)
      .Background(_fill, _radius);

  private View Side(ConditionSlotViewModel slot) =>
    Button(Held(slot, 84).Frame(maxWidth: float.PositiveInfinity, height: _side), () => slot.Enter, ButtonStyle.Plain)
      .ButtonStyle(new ButtonSurfaceStyle(_noEdge, 12, hovered: _hover))
      .Overlay(
        RoundedRectangle(12).Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false)
      )
      .Named("Condition slot " + slot.Slot)
      .Frame(maxWidth: float.PositiveInfinity, height: _side);

  // The comparison, in a round key between what it compares; a condition that gives one shows as the type's mark.
  private View Sign(ConditionSlotViewModel slot) =>
    Button(
        Switch(
            () => slot.Sign.Value,
            sign => Image(ElementIcons.Of(IconOf(sign)), _signIcon).Foreground(sign == '\0' ? _muted : _accent)
          )
          .Frame(width: _sign, height: _sign),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(FieldLook.Chip, _sign / 2))
      .Overlay(Capsule().Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false))
      .Named("Condition slot " + slot.Slot)
      .Frame(width: _sign, height: _sign);

  /// <summary>The icon a comparison is drawn as; the comparison type's mark for none.</summary>
  public static UiIconKind IconOf(char sign) =>
    sign switch
    {
      '<' => UiIconKind.Less,
      '≤' => UiIconKind.LessOrSame,
      '=' => UiIconKind.Same,
      '≠' => UiIconKind.NotSame,
      '≥' => UiIconKind.MoreOrSame,
      '>' => UiIconKind.More,
      _ => UiIconKind.CompareType,
    };

  // The lines from the bar to the answers; the way the condition goes now is drawn over them in the highlight color.
  private View Fork()
  {
    ConditionSlotViewModel yes = VM.Slots[OverlayPiece.Result],
      no = VM.Slots[OverlayPiece.Default];
    return ZStack(
        Alignment.TopLeading,
        Line(Stem, 0, Stem, _bend, Stroke, _path),
        Line(LeftDrop, _bend, RightDrop, _bend, Stroke, _path),
        Line(LeftDrop, _bend, LeftDrop, _fork, Stroke, _path),
        Line(RightDrop, _bend, RightDrop, _fork, Stroke, _path),
        If(() => yes.Live.Value, () => Way(LeftDrop)),
        If(() => no.Live.Value, () => Way(RightDrop))
      )
      .Frame(width: Width, height: _fork, alignment: Alignment.TopLeading);
  }

  // From the stem out to one answer.
  private View Way(float drop) =>
    ZStack(
        Alignment.TopLeading,
        Line(Stem, 0, Stem, _bend, Stroke, _accent),
        Line(Mathf.Min(Stem, drop), _bend, Mathf.Max(Stem, drop), _bend, Stroke, _accent),
        Line(drop, _bend, drop, _fork, Stroke, _accent)
      )
      .Frame(width: Width, height: _fork, alignment: Alignment.TopLeading);

  // A straight line, upright or level, from one point to another: its ends are square, so corners meet.
  private static View Line(float x, float y, float toX, float toY, float width, UiColor color)
  {
    float half = width / 2;
    return Rectangle()
      .Fill(color)
      .Frame(width: toX - x + width, height: toY - y + width)
      .Offset(x: x - half, y: y - half);
  }

  private View Answers() =>
    HStack(spacing: TileGap, Tile(VM.Slots[OverlayPiece.Result]), Tile(VM.Slots[OverlayPiece.Default]))
      .Frame(width: Width, height: _tile);

  // An answer: its word in the corner, what it gives in the middle.
  private View Tile(ConditionSlotViewModel slot) =>
    Button(
        ZStack(
            Held(slot, TileWidth - 60),
            Text(slot.Word)
              .Font(Font.Caption.Sized(12))
              .Foreground(() => slot.Live.Value ? _accent : _muted)
              .LineLimit(1)
              .Opacity(() => slot.Quiet.Value ? .5f : 1f)
              .Padding(new Insets(9, 12, 0, 0))
              .Frame(
                maxWidth: float.PositiveInfinity,
                maxHeight: float.PositiveInfinity,
                alignment: Alignment.TopLeading
              )
          )
          .Frame(maxWidth: float.PositiveInfinity, height: _tile),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => slot.Live.Value ? _live : _fill, _radius))
      .Overlay(
        RoundedRectangle(_radius)
          .Stroke(
            () =>
              slot.Bad.Value ? _bad
              : slot.Live.Value ? _accent
              : _noEdge,
            FieldLook.Edge
          )
          .AllowsHitTesting(false)
      )
      .Named("Condition slot " + slot.Slot)
      .Frame(maxWidth: float.PositiveInfinity, height: _tile);

  // What a slot holds: nothing yet (its mark and name), pills, one large number or word, or one color.
  private View Held(ConditionSlotViewModel slot, float widest) =>
    Switch(
      () => slot.Look.Value,
      look =>
        look switch
        {
          // The color over its name, both on the tile's middle.
          ConditionSlotLook.Color => VStack(
            spacing: _between,
            HAlignment.Center,
            RoundedRectangle(_swatchRadius)
              .Fill(() => slot.Swatch.Value)
              .Named("Condition swatch")
              .Frame(width: _swatch, height: _swatch)
              .Overlay(RoundedRectangle(_swatchRadius).Stroke(() => _swatchEdge, FieldLook.Edge)),
            // The way not taken is quieter, but a color stays the color it is.
            Text(() => slot.Caption.Value)
              .Font(Font.Caption.Sized(13))
              .LineLimit(1)
              .Opacity(() => slot.Quiet.Value ? .5f : 1f)
              .Named("Condition caption")
          ),
          ConditionSlotLook.Big => Switch(
              () => slot.BigSize.Value,
              size => Text(() => slot.Big.Value).Font(Font.Body.Sized(size).Bolded()).LineLimit(1)
            )
            .Opacity(() => slot.Quiet.Value ? .5f : 1f),
          ConditionSlotLook.Chips => HStack(
              spacing: 4,
              ForEach(() => slot.Chips.Value, chip => chip.Identity, chip => new FieldChipView(chip, true, widest))
            )
            .Opacity(() => slot.Quiet.Value ? .5f : 1f),
          _ => HStack(
              spacing: 6,
              Switch(() => slot.Type.Value, type => TypeMarks.Mark(type, 16)),
              Text(slot.Name).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1)
            )
            .Opacity(() => slot.Quiet.Value ? .5f : 1f),
        }
    );
}
