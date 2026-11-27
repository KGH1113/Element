using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Dock.Condition;
using Element.Presentation.OverlayEditor.Parts.Dock.Digits;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using TMPro;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The input method of what is picked, at the bottom of the right column and
/// always as large: the keypad for a number, a field the keyboard types
/// words into, two keys for true or false, the six comparisons, the nine
/// points, a condition's five slots, a value's own number at each count of
/// decimals. A solid color's picker and the font
/// list stand in the same place. With only a text's caret, a keyboard says
/// where the typing goes. Inside a condition's slot, that condition stays
/// on top folded into a strip (<see cref="ConditionStripView"/>), and the
/// slot's input method has the rest, grown out of its place on the strip.
/// Under each, the row of Remove and Apply.
/// </summary>
internal sealed class InputMethodView : View<InputDockViewModel>
{
  /// <summary>What an input method has under a folded condition's strip.</summary>
  public const float FoldedHeight = KeypadView.MethodHeight - ConditionStripView.Height - ConditionStripView.Gap;

  private const float WordsHeight = 52;

  public InputMethodView(InputDockViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 0,
        Switch(() => VM.Folded.Value, folded => folded ? Folded() : Place(KeypadView.MethodHeight))
          .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Top),
        KeypadView.SharedRow(VM.Keypad).Padding(new Insets(KeypadView.Gap, 0, 0, 0))
      )
      .Frame(width: KeypadView.Width, height: KeypadView.Height);

  // Inside a condition's slot: the condition folded on top, the slot's input method under it.
  private View Folded() =>
    VStack(
      spacing: ConditionStripView.Gap,
      new ConditionStripView(VM.Fold).Offset(y: () => VM.StripY).Opacity(() => VM.StripOpacity),
      Place(FoldedHeight)
    );

  // The input method in the place it has. Going into a slot it grows out of that slot's
  // place on the strip; coming back out, the condition settles back around it.
  private View Place(float height) =>
    Switch(() => VM.Method.Value, method => Method(method, height))
      .Frame(width: KeypadView.Width, height: height, alignment: Alignment.Top)
      .ScaleEffect(() => VM.BodyScale)
      .Offset(x: () => VM.BodyX, y: () => VM.BodyY)
      .Opacity(() => VM.BodyOpacity)
      .Named("Input method body");

  private View Method(DockMethod method, float height) =>
    method switch
    {
      DockMethod.Rest => VM.Keyboard ? Typing() : Clear(height),
      DockMethod.Words => Words(),
      DockMethod.Switch => TrueOrFalse(),
      DockMethod.Compare => Compares(),
      DockMethod.Condition => new ConditionFormView(VM.Form, height),
      DockMethod.Points => new GrowKeysView(VM.Points, height),
      DockMethod.Digits => new DigitsView(VM.Digits, VM.Keypad, height),
      // The color picker itself stands here, over this place; so does the font list. A gradient's tool is above.
      DockMethod.Color or DockMethod.Fonts or DockMethod.Gradient => Clear(height),
      _ => new KeypadView(VM.Keypad, KeypadView.KeyHeightIn(height)),
    };

  private static View Clear(float height) => Spacer().Frame(width: KeypadView.Width, height: height);

  // The text itself has the keys: a keyboard, and three words.
  private static View Typing() =>
    VStack(
        spacing: 10,
        HAlignment.Center,
        Image(ElementIcons.Of(UiIconKind.Keyboard), 46).Foreground(Colors.Muted),
        UI.Text(L("키보드로 입력", "Type on the keyboard"))
          .Font(Font.Caption.Sized(13))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Named("Input rest")
      )
      .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Center);

  // Words, typed on the keyboard into the picked piece: a field that reads as one, its type's mark first, a
  // ring in the highlight color while it has the keys, and the caret in it from the moment the words are picked.
  private View Words() =>
    HStack(
        spacing: 10,
        TypeMarks.Mark(OverlayValueType.Text, 20),
        TextField(Binding.Create(() => VM.Words, value => VM.Words = value), L("글자", "Words"))
          .TextFieldStyle(TextFieldStyle.Plain)
          // What is there stays as the caret comes in after it, and Esc only lets go of the keys: each letter is kept as typed.
          .SelectAllOnFocus(false)
          .RestoreOriginalTextOnEscape(false)
          .FocusOn(() => VM.WordsFocus.Value)
          .OnFocusChanged(VM.FocusWords)
          .Font(Font.Body.Sized(18))
          .Configure<TMP_InputField>(Caret)
          .Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity)
          .Named("Words field")
      )
      .Padding(new Insets(0, 12, 0, 12))
      .Frame(maxWidth: float.PositiveInfinity, height: WordsHeight)
      .Background(VM.Rest, 14)
      .Overlay(
        RoundedRectangle(14)
          .Stroke(() => VM.WordsFocused.Value ? VM.AccentInk : VM.FieldEdge, FieldLook.Ring)
          .Named("Words box")
          .AllowsHitTesting(false)
      );

  // The editors' caret, at the end of what is there when the field takes the keys.
  private static void Caret(TMP_InputField field)
  {
    UiCaret.Show(field);
    field.onSelect.AddListener(_ => field.MoveTextEnd(false));
  }

  private View TrueOrFalse() =>
    HStack(spacing: KeypadView.Gap, Choice(L("참", "True"), true), Choice(L("거짓", "False"), false));

  private View Choice(string label, bool value) =>
    Button(
        // A Hangul syllable with a final consonant reads a little high; a point lower centres it.
        UI.Text(label)
          .Font(Font.Body.Sized(18))
          .LineLimit(1)
          .TextAlignment(TextAlign.Center)
          .Padding(new Insets(2, 0, 0, 0))
          .Frame(maxWidth: float.PositiveInfinity, height: 72, alignment: Alignment.Center),
        () => VM.SetSwitch(value),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.SwitchOn.Value == value ? VM.Lit : VM.Rest, 14))
      // The one that holds wears the highlight color's outline too, as a lit field does.
      .Overlay(
        RoundedRectangle(14)
          .Stroke(() => VM.SwitchOn.Value == value ? VM.AccentInk : VM.NoEdge, FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named(value ? "Switch true" : "Switch false");

  // The six comparisons, drawn as icons so none reads as a bracket.
  private View Compares() =>
    Grid(
      3,
      KeypadView.Gap,
      Compare('<', UiIconKind.Less),
      Compare('≤', UiIconKind.LessOrSame),
      Compare('=', UiIconKind.Same),
      Compare('≠', UiIconKind.NotSame),
      Compare('≥', UiIconKind.MoreOrSame),
      Compare('>', UiIconKind.More)
    );

  private View Compare(char op, UiIconKind icon) =>
    Button(
        Image(ElementIcons.Of(icon), 24)
          .Foreground(() => VM.CompareOn.Value == op ? VM.OnInk : VM.AccentInk)
          .Frame(maxWidth: float.PositiveInfinity, height: KeypadView.PadKeyHeight),
        () => VM.PressCompare(op),
        ButtonStyle.Plain
      )
      .ButtonStyle(
        new ButtonSurfaceStyle(() => VM.CompareOn.Value == op ? VM.Lit : VM.Rest, KeypadView.PadKeyHeight / 2)
      )
      .Overlay(
        Capsule()
          .Stroke(() => VM.CompareOn.Value == op ? VM.AccentInk : VM.NoEdge, FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named("Key " + op);
}
