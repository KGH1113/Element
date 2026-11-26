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
    QuestionHeight = 64,
    SideHeight = 48,
    SignSize = 44,
    ForkHeight = 22,
    TileGap = 8,
    TileHeight = KeypadView.MethodHeight - QuestionHeight - ForkHeight,
    TileWidth = (Width - TileGap) / 2,
    // The fork: a stem down from the middle of the bar, out to the middle of each tile.
    Stem = Width / 2,
    LeftDrop = TileWidth / 2,
    RightDrop = Width - TileWidth / 2,
    Bend = 9,
    Stroke = 2;

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

  public ConditionFormView(ConditionFormViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(spacing: 0, Question(), Fork(), Answers())
      // "No" is a shake, not a sentence.
      .Offset(x: () => VM.Shake.X)
      .Frame(width: Width, height: KeypadView.MethodHeight, alignment: Alignment.Top);

  // What is compared: value, comparison, value, in one bar.
  private View Question() =>
    HStack(
        spacing: 4,
        Side(VM.Slots[OverlayPiece.Left]),
        Sign(VM.Slots[OverlayPiece.Compare]),
        Side(VM.Slots[OverlayPiece.Right])
      )
      .Padding(0, 8)
      .Frame(width: Width, height: QuestionHeight)
      .Background(_fill, 16);

  private View Side(ConditionSlotViewModel slot) =>
    Button(
        Held(slot, 84).Frame(maxWidth: float.PositiveInfinity, height: SideHeight),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(_noEdge, 12, hovered: _hover))
      .Overlay(
        RoundedRectangle(12).Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false)
      )
      .Named("Condition slot " + slot.Slot)
      .Frame(maxWidth: float.PositiveInfinity, height: SideHeight);

  // The comparison, in a round key between what it compares; a condition that gives one shows as the type's mark.
  private View Sign(ConditionSlotViewModel slot) =>
    Button(
        Switch(
            () => slot.Sign.Value,
            sign => Image(ElementIcons.Of(IconOf(sign)), 26).Foreground(sign == '\0' ? _muted : _accent)
          )
          .Frame(width: SignSize, height: SignSize),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(FieldLook.Chip, SignSize / 2))
      .Overlay(Capsule().Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false))
      .Named("Condition slot " + slot.Slot)
      .Frame(width: SignSize, height: SignSize);

  private static UiIconKind IconOf(char sign) =>
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
        Line(Stem, 0, Stem, Bend, Stroke, _path),
        Line(LeftDrop, Bend, RightDrop, Bend, Stroke, _path),
        Line(LeftDrop, Bend, LeftDrop, ForkHeight, Stroke, _path),
        Line(RightDrop, Bend, RightDrop, ForkHeight, Stroke, _path),
        If(() => yes.Live.Value, () => Way(LeftDrop)),
        If(() => no.Live.Value, () => Way(RightDrop))
      )
      .Frame(width: Width, height: ForkHeight, alignment: Alignment.TopLeading);
  }

  // From the stem out to one answer.
  private View Way(float drop) =>
    ZStack(
        Alignment.TopLeading,
        Line(Stem, 0, Stem, Bend, Stroke, _accent),
        Line(Mathf.Min(Stem, drop), Bend, Mathf.Max(Stem, drop), Bend, Stroke, _accent),
        Line(drop, Bend, drop, ForkHeight, Stroke, _accent)
      )
      .Frame(width: Width, height: ForkHeight, alignment: Alignment.TopLeading);

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
      .Frame(width: Width, height: TileHeight);

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
          .Frame(maxWidth: float.PositiveInfinity, height: TileHeight),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => slot.Live.Value ? _live : _fill, 16))
      .Overlay(
        RoundedRectangle(16)
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
      .Frame(maxWidth: float.PositiveInfinity, height: TileHeight);

  // What a slot holds: nothing yet (its mark and name), pills, one large number or word, or one color.
  private View Held(ConditionSlotViewModel slot, float widest) =>
    Switch(
      () => slot.Look.Value,
      look =>
        look switch
        {
          // The color over its name, both on the tile's middle.
          ConditionSlotLook.Color => VStack(
            spacing: 8,
            HAlignment.Center,
            RoundedRectangle(14)
              .Fill(() => slot.Swatch.Value)
              .Named("Condition swatch")
              .Frame(width: 46, height: 46)
              .Overlay(RoundedRectangle(14).Stroke(() => _swatchEdge, FieldLook.Edge)),
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
