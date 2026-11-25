using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Formula;
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
/// the six comparisons for a condition's operator, a condition's five slots
/// to go into, a group to go into, a made value's name and variable, or a
/// word that colors are picked above.
/// Under each, the row of AC, back and Apply.
/// </summary>
internal sealed class InputMethodView : View<InputDockViewModel>
{
  private const float RowHeight = 36,
    RowGap = 5;

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
      DockMethod.Condition => Slots(),
      DockMethod.Group => GroupInside(),
      DockMethod.Custom => CustomInside(),
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

  // A condition's five slots as a sentence, top to bottom; a press goes into one.
  private View Slots() => VStack(spacing: RowGap, Slot(0), Slot(1), Slot(2), Slot(3), Slot(4));

  private View Slot(int slot) =>
    Button(
        HStack(
            spacing: 8,
            UI.Text(FormulaRefusals.SlotName(slot))
              .Font(Font.Caption.Sized(12))
              .Foreground(Colors.Muted)
              .LineLimit(1)
              .Frame(width: 100, alignment: Alignment.Leading),
            UI.Text(() => VM.SlotText[slot].Value).RichText().Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer(minLength: 4),
            Image(ElementIcons.Of(UiIconKind.Forward), 12).Foreground(Colors.Muted)
          )
          .Padding(new Insets(0, 10, 0, 8))
          .Frame(maxWidth: float.PositiveInfinity, height: RowHeight),
        () => VM.EnterSlot(slot),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.SlotEmpty[slot].Value ? VM.Warn : VM.Rest, 9))
      .Named("Slot " + slot);

  private View GroupInside() =>
    VStack(spacing: 8, Title(L("묶음은 칸 안의 칸이에요", "A group is a variable inside the variable")), OpenInside());

  // A made value: its name, typed on the keyboard, and its variable to go into.
  private View CustomInside() =>
    VStack(
      spacing: 8,
      Title(L("내 값은 모든 요소가 같이 써요", "Every element shares your values")),
      TextField(Binding.Create(() => VM.CustomName, value => VM.CustomName = value), L("이름", "Name"))
        .Font(Font.Body.Sized(16))
        .Frame(maxWidth: float.PositiveInfinity, height: 40)
        .Named("Custom name"),
      OpenInside()
    );

  // The variable inside the picked piece, in brief, which a press opens.
  private View OpenInside() =>
    Button(
        HStack(
            spacing: 8,
            UI.Text(() => VM.SlotText[0].Value).RichText().Font(Font.Caption.Sized(13)).LineLimit(1),
            Spacer(minLength: 4),
            UI.Text(L("안으로", "Open")).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1),
            Image(ElementIcons.Of(UiIconKind.Forward), 12).Foreground(Colors.Muted)
          )
          .Padding(new Insets(0, 10, 0, 8))
          .Frame(maxWidth: float.PositiveInfinity, height: 48),
        () => VM.EnterSlot(0),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.SlotEmpty[0].Value ? VM.Warn : VM.Rest, 10))
      .Named("Slot 0");

  private static View Note(string title, string body) =>
    VStack(
        spacing: 6,
        alignment: HAlignment.Leading,
        UI.Text(title).Font(Font.Body.Sized(15)).LineLimit(2),
        UI.Text(body).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(3)
      )
      .Padding(new Insets(8, 4, 0, 4));
}
