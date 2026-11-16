using System;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The input dock: the editor's notice for a few seconds, the formula line
/// across the top over a thin line, then the values on the left (a search
/// and a list of places beside the open place's chips,
/// <see cref="OverlayValueBrowserView"/>) and the keypad on the right.
/// No card behind it.
/// </summary>
internal sealed class OverlayInputDockView : View<OverlayInputDockModel>
{
  public const float NoticeHeight = 26,
    LineGap = 10,
    KeypadGap = 32;

  private readonly OverlayEditorFrameModel _notices;
  private readonly OverlayFormulaLine _line;
  private readonly float _width;

  /// <param name="line">The formula line, which the dock asks where a value is dropped.</param>
  /// <param name="width">The dock's width.</param>
  /// <param name="measure">How wide words are at the dock's 13-unit size, for the list of places.</param>
  /// <param name="notices">The editor's notices, shown above the formula line.</param>
  public OverlayInputDockView(
    OverlayInputDockModel vm,
    OverlayEditorFrameModel notices,
    OverlayFormulaLine line,
    float width,
    Func<string, float> measure,
    bool reduceMotion
  )
    : base(vm)
  {
    _notices = notices;
    _line = line;
    _width = width;
    Values = new OverlayValueBrowserView(vm.Values, vm, measure, reduceMotion);
  }

  /// <summary>The values beside the keypad.</summary>
  public OverlayValueBrowserView Values { get; }

  public override View Body =>
    VStack(
        spacing: 0,
        UI.Text(() => _notices.Line.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => _notices.Ink.Value)
          .LineLimit(1)
          .Named("Notice")
          // Over the values, clear of the picker above the keypad.
          .Frame(
            width: _width - OverlayKeypadView.Width - KeypadGap,
            height: NoticeHeight,
            alignment: Alignment.Leading
          )
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        _line,
        HairLine.Across(() => VM.Underline.Value),
        HStack(spacing: KeypadGap, alignment: VAlignment.Top, Values, new OverlayKeypadView(VM.Keypad))
          .Padding(new Insets(LineGap - 1, 0, 0, 0))
      )
      .Frame(width: _width);
}
