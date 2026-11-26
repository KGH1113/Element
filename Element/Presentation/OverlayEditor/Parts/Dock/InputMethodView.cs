using Element.Presentation.OverlayEditor.Parts.Dock.Condition;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The input method of what is picked, at the bottom of the right column and
/// always as large: the keypad for a number, a field the keyboard types
/// words into, two keys for true or false, the six comparisons, the nine
/// points, a condition's five slots. A solid color's picker and the font
/// list stand in the same place. With only a text's caret, a keyboard says
/// where the typing goes. Under each, the row of Remove and Apply.
/// </summary>
internal sealed class InputMethodView : View<InputDockViewModel>
{
  public InputMethodView(InputDockViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 0,
        Switch(() => VM.Method.Value, Method)
          .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Top),
        KeypadView.SharedRow(VM.Keypad).Padding(new Insets(KeypadView.Gap, 0, 0, 0))
      )
      .Frame(width: KeypadView.Width, height: KeypadView.Height);

  private View Method(DockMethod method) =>
    method switch
    {
      DockMethod.Rest => VM.Keyboard ? Typing() : Clear(),
      DockMethod.Words => Words(),
      DockMethod.Switch => TrueOrFalse(),
      DockMethod.Compare => Compares(),
      DockMethod.Condition => new ConditionFormView(VM.Form),
      DockMethod.Points => new GrowKeysView(VM.Points),
      // The color picker itself stands here, over this place; so does the font list. A gradient's tool is above.
      DockMethod.Color or DockMethod.Fonts or DockMethod.Gradient => Clear(),
      _ => new KeypadView(VM.Keypad),
    };

  private static View Clear() => Spacer().Frame(width: KeypadView.Width, height: KeypadView.MethodHeight);

  // The text itself has the keys: a keyboard, and three words.
  private static View Typing() =>
    VStack(
        spacing: 10,
        Image(ElementIcons.Of(UiIconKind.Keyboard), 46).Foreground(Colors.Muted),
        UI.Text(L("키보드로 입력", "Type on the keyboard"))
          .Font(Font.Caption.Sized(13))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Named("Input rest")
      )
      .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Center);

  // Words, typed on the keyboard into the picked piece.
  private View Words() =>
    TextField(Binding.Create(() => VM.Words, value => VM.Words = value), L("글자", "Words"))
      .Font(Font.Body.Sized(18))
      .Frame(maxWidth: float.PositiveInfinity, height: 48)
      .Named("Words field");

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
