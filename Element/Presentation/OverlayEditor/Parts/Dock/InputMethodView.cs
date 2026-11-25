using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
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
/// The dock's input method, as large as the keypad and in its place: the
/// keypad for numbers, a field the keyboard types words into, true or false,
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
        spacing: KeypadView.Gap,
        Switch(() => VM.Method.Value, Method)
          .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.Top),
        KeypadView.SharedRow(VM.Keypad)
      )
      .Frame(width: KeypadView.Width, height: KeypadView.Height);

  private View Method(DockMethod method) =>
    method switch
    {
      DockMethod.Words => Words(),
      DockMethod.Switch => TrueOrFalse(),
      DockMethod.Compare => Compares(),
      DockMethod.Condition => Note(
        L("조건은 다섯 자리예요", "A condition has five places"),
        L(
          "위 줄에서 자리를 눌러 값을 넣어요. 자리마다 들어갈 타입이 써 있어요. 비교 기호를 누르면 여섯 가지 중에 골라요.",
          "Click a place on the line above to put a value in; each says its type. Click the sign to pick one of six."
        )
      ),
      DockMethod.Group => Note(
        L("계산은 위 줄에 펼쳐져 있어요", "The calculation is laid out on the line"),
        L("위 줄에서 조각을 눌러 고쳐요", "Click a piece on the line above to edit it")
      ),
      DockMethod.Custom => CustomName(),
      DockMethod.Color => Note(
        L("색은 위의 색 고르기에서 골라요", "Pick the color in the picker above"),
        L("하이라이트 색은 왼쪽 '색'에 있어요", "The highlight color is under Colors on the left")
      ),
      _ => new KeypadView(VM.Keypad),
    };

  private static View Title(string text) =>
    UI.Text(text)
      .Font(Font.Caption.Sized(12))
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .Frame(maxWidth: float.PositiveInfinity, height: 18, alignment: Alignment.Leading);

  // Words, typed on the keyboard into the picked piece.
  private View Words() =>
    VStack(
      spacing: 8,
      Title(L("키보드로 글자를 입력해요", "Type the words on the keyboard")),
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
      Title(L("참이나 거짓을 골라요", "Pick true or false")),
      HStack(spacing: KeypadView.Gap, Choice(L("참", "True"), true), Choice(L("거짓", "False"), false))
    );

  private View Choice(string label, bool value) =>
    Button(
        UI.Text(label).Font(Font.Body.Sized(18)).LineLimit(1).Frame(maxWidth: float.PositiveInfinity, height: 64),
        () => VM.SetSwitch(value),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.SwitchOn.Value == value ? VM.Lit : VM.Rest, 12))
      .Named(value ? "Switch true" : "Switch false");

  // The six comparisons, drawn as icons so none reads as a bracket.
  private View Compares() =>
    VStack(
      spacing: 8,
      Title(L("어떻게 비교할지 골라요", "Pick how to compare")),
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
      .Named("Key " + op);

  // A made value: its name, typed on the keyboard; every element shares it.
  private View CustomName() =>
    VStack(
      spacing: 8,
      Title(L("내 값은 모든 요소가 같이 써요", "Every element shares your values")),
      TextField(Binding.Create(() => VM.CustomName, value => VM.CustomName = value), L("이름", "Name"))
        .Font(Font.Body.Sized(16))
        .Frame(maxWidth: float.PositiveInfinity, height: 40)
        .Named("Custom name"),
      Button(L("안에 든 값 고치기", "Change what it holds"), VM.OpenMade, ButtonStyle.Standard)
        .Frame(maxWidth: float.PositiveInfinity, height: 36)
        .Named("Open custom")
    );

  private static View Note(string title, string body) =>
    VStack(
        spacing: 6,
        alignment: HAlignment.Leading,
        UI.Text(title).Font(Font.Body.Sized(15)).LineLimit(2),
        UI.Text(body).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(3)
      )
      .Padding(new Insets(8, 4, 0, 4));
}
