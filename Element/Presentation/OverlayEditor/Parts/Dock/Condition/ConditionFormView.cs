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
/// one bar reads "if" and holds what is compared: a value, the comparison in
/// a round key, a value. Lines run down from it to the two answers, which
/// stand side by side as tiles ("then", "otherwise"): a color shows as the
/// color, a number as the number, large. Where each line meets its tile, a
/// round mark says which answer is which without a word: ✓ for when it
/// holds, ✕ for when it doesn't. Both ways always look the same; nothing
/// lights up with the values of the moment. Each part is a click into its slot.
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
    _mark,
    _markIcon,
    _wordTop,
    _rowTop,
    _tile,
    _swatch,
    _swatchRadius,
    _radius,
    _between;

  private readonly UiColor _fill = UiPalette.EditorControl;
  private readonly UiColor _accent = UiPalette.SoftAccent;
  private readonly UiColor _muted = UiPalette.Muted;
  private readonly UiColor _bad = UiPalette.Error;
  private readonly UiColor _noEdge = FieldLook.Clear;
  private readonly UiColor _hover = new Color(1, 1, 1, .05f);
  private readonly UiColor _tileHover = UiPalette.Wash(UiPalette.EditorDim, .1f);

  // Both ways alike, plain to see on the dark.
  private readonly UiColor _path = new Color(1, 1, 1, .42f);
  private readonly UiColor _swatchEdge = new Color(1, 1, 1, .28f);

  /// <param name="height">How tall a place it has; lower than the input method's whole place, it is drawn smaller.</param>
  public ConditionFormView(ConditionFormViewModel vm, float height = KeypadView.MethodHeight)
    : base(vm)
  {
    bool small = height < KeypadView.MethodHeight;
    _height = height;
    // A little taller than its slots need, so its word has a corner of its own above them.
    _question = small ? 60 : 72;
    _wordTop = small ? 5 : 7;
    _rowTop = small ? 18 : 22;
    _side = small ? 34 : 40;
    _sign = small ? 36 : 44;
    _signIcon = small ? 21 : 26;
    // Room on each way, under its bend, for the mark that says which answer it leads to.
    _fork = small ? 28 : 34;
    _bend = small ? 7 : 9;
    _mark = small ? 16 : 20;
    _markIcon = small ? 10 : 12;
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

  // What is compared: value, comparison, value, in one bar; "if" in its corner, as each answer has its word in its own.
  private View Question() =>
    ZStack(
        HStack(
            spacing: 4,
            Side(VM.Slots[OverlayPiece.Left]),
            Sign(VM.Slots[OverlayPiece.Compare]),
            Side(VM.Slots[OverlayPiece.Right])
          )
          // Under the corner its word has, so nothing covers it.
          .Padding(new Insets(_rowTop, 8, 0, 8))
          .Frame(width: Width, height: _question, alignment: Alignment.Top),
        Corner(VM.Start)
      )
      .Frame(width: Width, height: _question)
      .Background(_fill, _radius);

  // A part's word, small in its top left corner: "if", "then", "otherwise".
  private View Corner(string word) =>
    Text(word)
      .Font(Font.Caption.Sized(12))
      .Foreground(_muted)
      .LineLimit(1)
      .Padding(new Insets(_wordTop, 12, 0, 0))
      .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity, alignment: Alignment.TopLeading)
      .AllowsHitTesting(false);

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

  // The lines from the bar to the answers, both alike, and on each a mark: ✓ to the answer when it holds, ✕ to the other.
  private View Fork() =>
    ZStack(
        Alignment.TopLeading,
        Line(Stem, 0, Stem, _bend, Stroke, _path),
        Line(LeftDrop, _bend, RightDrop, _bend, Stroke, _path),
        Line(LeftDrop, _bend, LeftDrop, _fork, Stroke, _path),
        Line(RightDrop, _bend, RightDrop, _fork, Stroke, _path),
        Mark(LeftDrop, UiIconKind.Check, "Condition mark true"),
        Mark(RightDrop, UiIconKind.Close, "Condition mark false")
      )
      .Frame(width: Width, height: _fork, alignment: Alignment.TopLeading);

  // A round mark on a way, under its bend, clear of the tile it leads to.
  private View Mark(float drop, UiIconKind icon, string name) =>
    Image(ElementIcons.Of(icon), _markIcon)
      .Foreground(Colors.Foreground)
      .Frame(width: _mark, height: _mark)
      .Background(FieldLook.Chip, _mark / 2)
      .Overlay(Circle().Stroke(_path, FieldLook.Edge))
      .Named(name)
      .Offset(x: drop - _mark / 2, y: _bend + (_fork - _bend - _mark) / 2);

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
        ZStack(Held(slot, TileWidth - 60), Corner(slot.Word)).Frame(maxWidth: float.PositiveInfinity, height: _tile),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(_fill, _radius, hovered: _tileHover))
      .Overlay(
        RoundedRectangle(_radius).Stroke(() => slot.Bad.Value ? _bad : _noEdge, FieldLook.Edge).AllowsHitTesting(false)
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
            Text(() => slot.Caption.Value).Font(Font.Caption.Sized(13)).LineLimit(1).Named("Condition caption")
          ),
          ConditionSlotLook.Big => Switch(
            () => slot.BigSize.Value,
            size => Text(() => slot.Big.Value).Font(Font.Body.Sized(size).Bolded()).LineLimit(1)
          ),
          ConditionSlotLook.Chips => HStack(
            spacing: 4,
            ForEach(() => slot.Chips.Value, chip => chip.Identity, chip => new FieldChipView(chip, true, widest))
          ),
          _ => HStack(
            spacing: 6,
            Switch(() => slot.Type.Value, type => TypeMarks.Mark(type, 16)),
            Text(slot.Name).Font(Font.Caption.Sized(13)).Foreground(Colors.Muted).LineLimit(1)
          ),
        }
    );
}
