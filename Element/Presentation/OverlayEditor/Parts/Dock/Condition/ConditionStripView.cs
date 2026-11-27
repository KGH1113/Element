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
/// The condition the dock is inside of, folded into one strip over the
/// input method: a round key back out, then its five slots in a row (what
/// is compared, the comparison, what it is compared with, and after a tick
/// the two answers), the one being filled lit. A click on another slot goes
/// straight there; the key unfolds the condition again.
/// </summary>
internal sealed class ConditionStripView : View<ConditionFormViewModel>
{
  /// <summary>How tall the strip is, and what it keeps clear under it.</summary>
  public const float Height = 40,
    Gap = 8;

  private const float Width = KeypadView.Width,
    Pad = 6,
    Between = 4,
    Key = 28,
    KeyAfter = 2,
    Answer = 30,
    // An answer's ✓ or ✕, small, just before it.
    MarkSize = 9,
    MarkGap = 2,
    MarkedAnswer = MarkSize + MarkGap + Answer,
    TickGap = 4,
    // The two sides share what the keys, the tick and the marked answers leave.
    Side = (Width - Pad * 2 - Key - KeyAfter - Key - MarkedAnswer * 2 - (TickGap * 2 + 1) - Between * 6) / 2,
    // A slot that isn't the one being filled reads quieter.
    Quiet = .62f;

  private readonly UiColor _fill = UiPalette.EditorControl;
  private readonly UiColor _live = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .17f);
  private readonly UiColor _accent = UiPalette.SoftAccent;
  private readonly UiColor _muted = UiPalette.Muted;
  private readonly UiColor _bad = UiPalette.Error;
  private readonly UiColor _noEdge = FieldLook.Clear;
  private readonly UiColor _hover = new Color(1, 1, 1, .06f);
  private readonly UiColor _keyHover = UiPalette.Wash(UiPalette.EditorDim, .21f);
  private readonly UiColor _tick = new Color(1, 1, 1, .18f);
  private readonly UiColor _swatchEdge = new Color(1, 1, 1, .3f);

  public ConditionStripView(ConditionFormViewModel vm)
    : base(vm) { }

  /// <summary>
  /// Where slot <paramref name="slot"/> stands on the strip, from its left
  /// edge to the slot's middle: what is in the slot grows out of there.
  /// </summary>
  public static float CenterOf(int slot)
  {
    float left = Pad + Key + KeyAfter + Between,
      sign = left + Side + Between,
      right = sign + Key + Between,
      yes = right + Side + Between + TickGap * 2 + 1 + Between + MarkSize + MarkGap,
      no = yes + Answer + Between + MarkSize + MarkGap;
    return slot == OverlayPiece.Left ? left + Side / 2
      : slot == OverlayPiece.Compare ? sign + Key / 2
      : slot == OverlayPiece.Right ? right + Side / 2
      : slot == OverlayPiece.Result ? yes + Answer / 2
      : no + Answer / 2;
  }

  public override View Body =>
    HStack(
        spacing: Between,
        Back(),
        Wide(VM.Slots[OverlayPiece.Left]),
        Sign(VM.Slots[OverlayPiece.Compare]),
        Wide(VM.Slots[OverlayPiece.Right]),
        Rectangle().Fill(_tick).Frame(width: 1, height: 18).Padding(0, TickGap),
        Marked(VM.Slots[OverlayPiece.Result]),
        Marked(VM.Slots[OverlayPiece.Default])
      )
      .Padding(0, Pad)
      .Frame(width: Width, height: Height)
      .Background(_fill, 13)
      // "No" is a shake, not a sentence.
      .Offset(x: () => VM.Shake.X)
      .Named("Condition strip");

  // Back out: the condition unfolds.
  private View Back() =>
    Button(
        Image(ElementIcons.Of(UiIconKind.Back), 14).Foreground(Colors.Foreground).Frame(width: Key, height: Key),
        () => VM.Leave,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(FieldLook.Chip, Key / 2, hovered: _keyHover))
      .Named("Condition back")
      .Frame(width: Key, height: Key)
      .Padding(new Insets(0, 0, 0, KeyAfter));

  // What is compared, or what it is compared with: its first value's mark and name.
  private View Wide(ConditionSlotViewModel slot) =>
    Slot(
      slot,
      Side,
      Switch(
        () => slot.Look.Value,
        look =>
          look == ConditionSlotLook.Empty
            ? Blank(slot)
            : HStack(
              spacing: 3,
              Switch(() => slot.Lead.Value, type => TypeMarks.Letter(type, 12)),
              // Words not typed yet: the hollow box they have everywhere.
              If(() => slot.LeadBlank.Value, () => BlankWords.Box(11.5f)),
              Text(() => slot.Brief.Value)
                .Font(Font.Caption.Sized(11.5f))
                .LineLimit(1)
                // What the slot leaves beside the mark.
                .Frame(maxWidth: Side - 19)
            )
      )
    );

  // An answer, with room for one thing: its color, its number, or the mark of what it holds.
  // An answer with its ✓ or ✕ just before it, as on the unfolded fork: ✓ for when the condition holds, ✕ for when it doesn't.
  private View Marked(ConditionSlotViewModel slot) =>
    HStack(
      spacing: MarkGap,
      Image(ElementIcons.Of(slot.Slot == OverlayPiece.Result ? UiIconKind.Check : UiIconKind.Close), MarkSize)
        .Foreground(Colors.Foreground)
        .Named(slot.Slot == OverlayPiece.Result ? "Condition strip mark true" : "Condition strip mark false"),
      Narrow(slot)
    );

  private View Narrow(ConditionSlotViewModel slot) =>
    Slot(
      slot,
      Answer,
      Switch(
        () => slot.Look.Value,
        look =>
          look switch
          {
            ConditionSlotLook.Color => RoundedRectangle(5)
              .Fill(() => slot.Swatch.Value)
              .Frame(width: 16, height: 16)
              .Overlay(RoundedRectangle(5).Stroke(() => _swatchEdge, 1)),
            ConditionSlotLook.Big => Text(() => slot.Big.Value)
              .Font(Font.Caption.Sized(11.5f).Bolded())
              .LineLimit(1)
              .Frame(maxWidth: Answer - 4),
            ConditionSlotLook.Chips => Switch(
              () => slot.Nested.Value,
              // A condition in an answer is a fork of its own.
              nested =>
                nested
                  ? Image(ElementIcons.Of(UiIconKind.Fork), 18).Foreground(Colors.Foreground)
                  : Switch(
                    () => slot.LeadBlank.Value,
                    blank =>
                      blank ? BlankWords.Box(11.5f) : Switch(() => slot.Lead.Value, type => TypeMarks.Letter(type, 12))
                  )
            ),
            _ => Blank(slot),
          }
      )
    );

  // An empty slot says what goes in it by its type's mark.
  private static View Blank(ConditionSlotViewModel slot) =>
    Switch(() => slot.Type.Value, type => TypeMarks.Letter(type, 12)).Opacity(.7f);

  // The comparison, round as it is on the unfolded condition.
  private View Sign(ConditionSlotViewModel slot) =>
    Button(
        Switch(
            () => slot.Sign.Value,
            sign =>
              Image(ElementIcons.Of(ConditionFormView.IconOf(sign)), 15).Foreground(sign == '\0' ? _muted : _accent)
          )
          .Frame(width: Key, height: Key),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.In.Value == slot.Slot ? _live : FieldLook.Chip, Key / 2))
      .Overlay(
        Capsule().Stroke(() => Edge(slot.Bad.Value, VM.In.Value == slot.Slot), FieldLook.Edge).AllowsHitTesting(false)
      )
      .Named("Condition strip slot " + slot.Slot)
      .Frame(width: Key, height: Key);

  // One slot on the strip: lit while it is the one being filled, a click away otherwise.
  private View Slot(ConditionSlotViewModel slot, float width, View held) =>
    Button(
        held.Opacity(() => VM.In.Value == slot.Slot ? 1f : Quiet).Frame(width: width, height: Key),
        () => slot.Enter,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.In.Value == slot.Slot ? _live : _noEdge, 9, hovered: _hover))
      .Overlay(
        RoundedRectangle(9)
          .Stroke(() => Edge(slot.Bad.Value, VM.In.Value == slot.Slot), FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named("Condition strip slot " + slot.Slot)
      .Frame(width: width, height: Key);

  [Pure]
  private UiColor Edge(bool bad, bool here) =>
    bad ? _bad
    : here ? _accent
    : _noEdge;
}
