using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The dock's input method, as large as the keypad and in its place: with
/// nothing picked a word on what shows there; the keypad for numbers, a field the keyboard types words into, true or false,
/// the six comparisons for a condition, a made value's name, or a word on
/// what to do (a condition's slots are on the line, colors are picked above).
/// Under each, the row of Remove and Apply.
/// </summary>
internal sealed class InputMethodView : View<InputDockViewModel>
{
  public InputMethodView(InputDockViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: 0,
        // Which input method this is, where the values beside it have their title.
        UI.Text(() => VM.MethodName.Value)
          .Font(Font.Caption.Sized(12))
          .Foreground(Colors.Muted)
          .LineLimit(1)
          .Named("Input method")
          .Padding(new Insets(3, 2, 0, 0))
          .Frame(width: KeypadView.Width, height: KeypadView.TitleHeight, alignment: Alignment.TopLeading),
        Switch(() => VM.Method.Value, Method)
          .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Top),
        KeypadView.SharedRow(VM.Keypad).Padding(new Insets(KeypadView.Gap, 0, 0, 0))
      )
      .Frame(width: KeypadView.Width, height: KeypadView.Height);

  private View Method(DockMethod method) =>
    method switch
    {
      DockMethod.Rest => Said(UI.Text(() => VM.RestNote.Value).Named("Input rest")),
      DockMethod.Words => Words(),
      DockMethod.Switch => TrueOrFalse(),
      DockMethod.Compare => Compares(),
      DockMethod.Condition => Note(
        L("조건은 다섯 자리예요.", "A condition has five places."),
        L(
          "위 줄에서 자리를 눌러 값을 넣어요. 자리마다 들어갈 타입이 써 있어요. 비교 기호를 누르면 여섯 가지 중에 골라요.",
          "Click a place on the line above to put a value in; each says its type. Click the sign to pick one of six."
        )
      ),
      DockMethod.Group => Note(
        L("계산은 위 줄에 펼쳐져 있어요.", "The calculation is laid out on the line."),
        L("위 줄에서 조각을 눌러 고쳐요.", "Click a piece on the line above to edit it.")
      ),
      DockMethod.Custom => CustomName(),
      // The color picker itself stands here, over this place; so do the font list and the nine points.
      DockMethod.Color or DockMethod.Fonts or DockMethod.Points => Spacer()
        .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight),
      DockMethod.Gradient => Note(
        L("그라데이션은 위에서 고쳐요.", "The gradient is changed above."),
        L(
          "색을 하나 누르면 그 색과 위치를 고치고, 각도는 미리보기에서 돌려요.",
          "Click a color to change it and where it sits; turn the angle on the preview."
        )
      ),
      _ => new KeypadView(VM.Keypad),
    };

  // Words, typed on the keyboard into the picked piece.
  private View Words() =>
    VStack(
      spacing: 8,
      TextField(Binding.Create(() => VM.Words, value => VM.Words = value), L("글자", "Words"))
        .Font(Font.Body.Sized(18))
        .Frame(maxWidth: float.PositiveInfinity, height: 44)
        .Named("Words field"),
      UI.Text(
          L(
            "빈칸도 글자예요. 다 쳤으면 적용이나 다른 조각을 눌러요",
            "Spaces count. When done, Apply or pick another piece"
          )
        )
        .Font(Font.Caption.Sized(11))
        .Foreground(Colors.Muted)
        .LineLimit(2)
    );

  private View TrueOrFalse() =>
    VStack(
      spacing: 8,
      HStack(spacing: KeypadView.Gap, Choice(L("참", "True"), true), Choice(L("거짓", "False"), false))
    );

  private View Choice(string label, bool value) =>
    Button(
        // A Hangul syllable with a final consonant reads a little high; a point lower centres it.
        UI.Text(label)
          .Font(Font.Body.Sized(18))
          .LineLimit(1)
          .TextAlignment(TextAlign.Center)
          .Padding(new Insets(2, 0, 0, 0))
          .Frame(maxWidth: float.PositiveInfinity, height: 64, alignment: Alignment.Center),
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
    VStack(
      spacing: 8,
      Grid(
        3,
        KeypadView.Gap,
        Compare('<', UiIconKind.Less),
        Compare('≤', UiIconKind.LessOrSame),
        Compare('=', UiIconKind.Same),
        Compare('≠', UiIconKind.NotSame),
        Compare('≥', UiIconKind.MoreOrSame),
        Compare('>', UiIconKind.More)
      )
    );

  private View Compare(char op, UiIconKind icon) =>
    Button(
        Image(ElementIcons.Of(icon), 22)
          .Foreground(() => VM.CompareOn.Value == op ? VM.OnInk : VM.AccentInk)
          .Frame(maxWidth: float.PositiveInfinity, height: KeypadView.KeyHeight),
        () => VM.PressCompare(op),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.CompareOn.Value == op ? VM.Lit : VM.Rest, KeypadView.KeyHeight / 2))
      .Overlay(
        Capsule()
          .Stroke(() => VM.CompareOn.Value == op ? VM.AccentInk : VM.NoEdge, FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named("Key " + op);

  // A made value: its name, typed on the keyboard; every element shares it.
  private View CustomName() =>
    VStack(
      spacing: 8,
      UI.Text(L("내 값은 모든 요소가 같이 써요", "Every element shares your values"))
        .Font(Font.Caption.Sized(12))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Frame(maxWidth: float.PositiveInfinity, height: 18, alignment: Alignment.Leading),
      TextField(Binding.Create(() => VM.CustomName, value => VM.CustomName = value), L("이름", "Name"))
        .Font(Font.Body.Sized(16))
        .Frame(maxWidth: float.PositiveInfinity, height: 40)
        .Named("Custom name"),
      Button(L("안에 든 값 고치기", "Change what it holds"), VM.OpenMade, ButtonStyle.Standard)
        .Frame(maxWidth: float.PositiveInfinity, height: 36)
        .Named("Open custom")
    );

  private static View Note(string title, string body) => Said(UI.Text(InputDockViewModel.Said(title, body)));

  // What a place without keys says: its first words bold in the foreground, the rest going on in a muted ink.
  private static View Said(View words) =>
    words
      .RichText()
      .Font(Font.Caption.Sized(13))
      .Foreground(Colors.Muted)
      .LineLimit(5)
      .Padding(new Insets(2, 2, 0, 4))
      .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading);
}
