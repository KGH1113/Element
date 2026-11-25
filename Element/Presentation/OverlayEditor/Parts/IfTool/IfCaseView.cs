using Element.Domain.Overlay.Conditions;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// One case's card. Open (its look is in the preview): the value, a seesaw
/// tilting as it compares now, the number, then the three ways the seesaw
/// can stand as switches for when the case is on, and its look. Closed: the
/// case as one sentence over its look, the whole card a button that opens
/// it. The case on now wears an accent edge and says so.
/// </summary>
internal sealed class IfCaseView : View<IfCaseViewModel>
{
  public const float Padding = 8,
    Gap = 6,
    ControlHeight = 32,
    ResultHeight = 46,
    LookHeight = 28,
    Radius = 10;

  public IfCaseView(ItemSlot<IfCaseViewModel> slot)
    : base(slot) { }

  public IfCaseView(IfCaseViewModel vm)
    : base(vm) { }

  private string Key => VM.IsOtherwise ? "otherwise" : "case " + VM.Index;

  public override View Body => If(() => VM.Open.Value, () => Marked(Opened()), () => Marked(Closed()));

  // The case on now wears an accent edge and a badge, whichever way its card stands.
  private View Marked(View card) =>
    card.Overlay(
        RoundedRectangle(Radius).Stroke(() => VM.Now.Value ? VM.Accent : VM.NoEdge, 1.5f).AllowsHitTesting(false)
      )
      .Overlay(NowBadge(), Alignment.TopTrailing);

  // The card whose look is in the preview: everything about its case can be changed here.
  private View Opened() =>
    VStack(spacing: Gap, VM.IsOtherwise ? Heading() : VStack(spacing: Gap, Comparison(), Results()), LookLine(true))
      .Padding(Padding)
      .Frame(maxWidth: float.PositiveInfinity)
      .Background(VM.Surface, Radius);

  // Any other card: what it says, and a press shows its look.
  private View Closed() =>
    Button(
        VStack(spacing: 2, Heading(), LookLine(false)).Padding(Padding).Frame(maxWidth: float.PositiveInfinity),
        () => VM.ShowLook,
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.Surface, Radius))
      .Named("Show " + Key);

  // "Otherwise", or a closed case in a sentence.
  private View Heading() =>
    (VM.IsOtherwise ? UI.Text(L("아니면", "Otherwise")) : UI.Text(() => VM.Sentence.Value))
      .Font(Font.Caption.Sized(12))
      .Foreground(Colors.Muted)
      .LineLimit(1)
      .Frame(maxWidth: float.PositiveInfinity, height: 18, alignment: Alignment.Leading)
      .Padding(new Insets(0, 2, 0, 34));

  // The value, the seesaw as it stands now, the number, and taking the case away.
  private View Comparison() =>
    HStack(
      spacing: 6,
      Button(
          HStack(
              spacing: 6,
              UI.Text(() => VM.ValueName.Value)
                .Font(Font.Caption.Sized(13))
                .Foreground(VM.Accent)
                .LineLimit(1)
                .LayoutPriority(1),
              UI.Text(() => VM.ValueNow.Value).Font(Font.Caption.Sized(12)).Foreground(Colors.Muted).LineLimit(1),
              Spacer(minLength: 0)
            )
            .Padding(new Insets(0, 10, 0, 8))
            .Frame(maxWidth: float.PositiveInfinity, height: ControlHeight),
          () => VM.PickValue,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(() => VM.ValueFill.Value, 8))
        .Named("If value " + VM.Index),
      new SeesawView(() => VM.Result.Value, 40, () => Colors.Foreground),
      Button(
          UI.Text(() => VM.Number.Value)
            .Font(Font.Caption.Sized(14))
            .Foreground(() => VM.NumberComputed.Value ? VM.Accent : Colors.Foreground)
            .LineLimit(1)
            .Frame(width: 64, height: ControlHeight),
          () => VM.EditNumber,
          ButtonStyle.Plain
        )
        .ButtonStyle(new ButtonSurfaceStyle(() => VM.NumberFill.Value, 8))
        .Named("If number " + VM.Index),
      Button(
          Image(ElementIcons.Of(UiIconKind.Close), 14).Foreground(Colors.Muted).Frame(width: 24, height: ControlHeight),
          () => VM.Remove,
          ButtonStyle.Plain
        )
        .Named("Remove case " + VM.Index)
    );

  // The three ways the seesaw can stand; the lit ones turn the case on.
  private View Results() =>
    HStack(
      spacing: 6,
      Result(OverlayComparison.Smaller, L("더 작으면", "Smaller"), "smaller"),
      Result(OverlayComparison.Same, L("같으면", "The same"), "same"),
      Result(OverlayComparison.Bigger, L("더 크면", "Bigger"), "bigger")
    );

  private View Result(OverlayComparison result, string label, string name) =>
    Button(
        VStack(
            spacing: 2,
            new SeesawView(() => result, 30, () => VM.TurnsOn(result) ? Colors.Foreground : Colors.Muted),
            UI.Text(label)
              .Font(Font.Caption.Sized(11))
              .Foreground(() => VM.TurnsOn(result) ? Colors.Foreground : Colors.Muted)
              .LineLimit(1)
          )
          .Frame(maxWidth: float.PositiveInfinity, height: ResultHeight)
          // A dot marks how the value compares right now.
          .Overlay(
            Circle().Fill(Colors.Success).Frame(width: 6, height: 6).Padding(5).Hidden(() => VM.Result.Value != result),
            Alignment.TopTrailing
          ),
        () => VM.Toggle(result),
        ButtonStyle.Plain
      )
      .ButtonStyle(new ButtonSurfaceStyle(() => VM.TurnsOn(result) ? VM.On : VM.Raised, 8))
      .Named("If " + name + " " + VM.Index);

  // The look in a line, in its own color; the open card says it is the one being edited.
  private View LookLine(bool open) =>
    HStack(
        spacing: 6,
        Image(ElementIcons.Of(UiIconKind.Forward), 12).Foreground(Colors.Muted),
        UI.Text(() => VM.LookText.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => VM.LookInk.Value)
          .LineLimit(1)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        open ? UI.Text(L("고치는 중", "Editing")).Font(Font.Caption.Sized(11)).Foreground(VM.Accent).LineLimit(1) : null
      )
      .Padding(new Insets(0, open ? 8 : 2, 0, open ? 8 : 2))
      .Frame(maxWidth: float.PositiveInfinity, height: open ? LookHeight : 20)
      .Background(open ? (UiColor)UiPalette.EditorDim : (UiColor)UnityEngine.Color.clear, 8);

  private View NowBadge() =>
    UI.Text(L("지금", "Now"))
      .Font(Font.Caption.Sized(10).Bolded())
      .Foreground((UiColor)UiPalette.EditorDim)
      .LineLimit(1)
      .Padding(new Insets(1, 7, 1, 7))
      .Background(Capsule().Fill((UiColor)UiPalette.SoftAccent))
      .Padding(new Insets(6, 0, 0, 8))
      .Hidden(() => !VM.Now.Value)
      .AllowsHitTesting(false);
}
