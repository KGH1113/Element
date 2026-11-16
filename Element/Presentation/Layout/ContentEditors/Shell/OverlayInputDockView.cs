using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using UnityEngine.UI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The input dock: the editor's notice for a few seconds, the formula line
/// across the top over a thin line, then
/// every value by category on the left (names in one column, each
/// category's chips wrapping to their right) and the keypad on the right.
/// No card behind it.
/// </summary>
internal sealed class OverlayInputDockView : View<OverlayInputDockModel>
{
  public const float NoticeHeight = 26,
    LineGap = 10,
    ChipHeight = 30,
    ChipGap = 8,
    CategoryGap = 10,
    KeypadGap = 32;

  private static readonly UiColor ChipFill = new Color(1, 1, 1, .07f),
    ChipInk = new Color(1, 1, 1, .92f);

  private readonly OverlayEditorFrameModel _notices;
  private readonly OverlayFormulaLine _line;
  private readonly float _width;
  private readonly float _column;
  private readonly bool _reduceMotion;

  /// <param name="line">The formula line, which the dock asks where a value is dropped.</param>
  /// <param name="width">The dock's width.</param>
  /// <param name="column">The width of the category names' column.</param>
  /// <param name="notices">The editor's notices, shown above the formula line.</param>
  public OverlayInputDockView(
    OverlayInputDockModel vm,
    OverlayEditorFrameModel notices,
    OverlayFormulaLine line,
    float width,
    float column,
    bool reduceMotion
  )
    : base(vm)
  {
    _notices = notices;
    _line = line;
    _width = width;
    _column = column;
    _reduceMotion = reduceMotion;
  }

  public override View Body =>
    VStack(
        spacing: 0,
        UI.Text(() => _notices.Line.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => _notices.Ink.Value)
          .LineLimit(1)
          .Named("Notice")
          // Over the values, clear of the picker above the keypad.
          .Frame(width: _width - OverlayKeypadView.Width - KeypadGap, height: NoticeHeight, alignment: Alignment.Leading)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        _line,
        HairLine.Across(() => VM.Underline.Value),
        HStack(spacing: KeypadGap, alignment: VAlignment.Top, Values(), new OverlayKeypadView(VM.Keypad))
          .Padding(new Insets(LineGap - 1, 0, 0, 0))
      )
      .Frame(width: _width);

  // Every value at once; the list scrolls when there are more than fit.
  private View Values() =>
    ScrollView(VStack(spacing: ChipGap + CategoryGap, Sections()))
      .Configure<ScrollRect>(scroll =>
      {
        scroll.scrollSensitivity = 34;
        scroll.inertia = !_reduceMotion;
      })
      .Frame(maxWidth: float.PositiveInfinity, height: OverlayKeypadView.Height);

  private View[] Sections()
  {
    View[] sections = new View[VM.Sections.Count];
    for (int i = 0; i < sections.Length; i++)
      sections[i] = Section(VM.Sections[i]);
    return sections;
  }

  private View Section(ValueSection section)
  {
    View[] chips = new View[section.Chips.Count];
    for (int i = 0; i < chips.Length; i++)
      chips[i] = Chip(section.Chips[i]);
    return HStack(
      spacing: 0,
      alignment: VAlignment.Top,
      UI.Text(section.Name)
        .Font(Font.Caption.Sized(13))
        .Foreground(Colors.Muted)
        .LineLimit(1)
        .Frame(width: _column, height: ChipHeight, alignment: Alignment.Leading),
      Flow(ChipGap, chips).Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.TopLeading)
    );
  }

  // A chip is pressed to pick its value and dragged to carry it out. The
  // label takes the chip's height, so the capsule, its press and its drag
  // are all as tall as the chip.
  private View Chip(ValueChipModel chip) =>
    Button(
        UI.Text(chip.Label)
          .Font(Font.Caption.Sized(13))
          .Foreground(ChipInk)
          .LineLimit(1)
          .Padding(0, 14)
          .Frame(height: ChipHeight),
        () => chip.Pick,
        ButtonStyle.Plain
      )
      .Background(ChipFill, ChipHeight / 2)
      .Configure<GameObject>(target => OverlayValueChipHandle.Attach(target, chip.Value, VM))
      .Named(chip.Name);
}
