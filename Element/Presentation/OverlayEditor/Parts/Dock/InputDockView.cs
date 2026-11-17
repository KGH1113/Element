using System;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The input dock: the editor's notice for a few seconds, the formula line
/// across the top over a thin line, then the values on the left (a search
/// and a list of places beside the open place's chips,
/// <see cref="ValueBrowserView"/>) and the keypad on the right.
/// No card behind it. Reads only its view model.
/// </summary>
internal sealed class InputDockView : View<InputDockViewModel>
{
  public const float NoticeHeight = 26,
    LineGap = 10,
    KeypadGap = 32;

  private readonly float _width;

  /// <param name="width">The dock's width.</param>
  public InputDockView(InputDockViewModel vm, float width)
    : base(vm)
  {
    _width = width;
    Line = new FormulaLineView(vm);
    Values = new ValueBrowserView(vm.Values);
  }

  /// <summary>The formula line, where a carried value joins the formula.</summary>
  private FormulaLineView Line { get; }

  /// <summary>The values beside the keypad.</summary>
  public ValueBrowserView Values { get; }

  public override View Body =>
    VStack(
        spacing: 0,
        UI.Text(() => VM.Notices.Line.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => VM.Notices.Ink.Value)
          .LineLimit(1)
          .Named("Notice")
          // Over the values, clear of the picker above the keypad.
          .Frame(width: _width - KeypadView.Width - KeypadGap, height: NoticeHeight, alignment: Alignment.Leading)
          .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Leading),
        // The line is where a carried value joins the formula; it lights up under it.
        Line.DropDestination<OverlayValueDefinition>(
          over: VM.OverFormula,
          drop: VM.DropOnFormula,
          targeted: VM.SetDrop,
          refused: VM.DropOnFormula
        ),
        HairLine.Across(() => VM.Underline.Value),
        HStack(spacing: KeypadGap, alignment: VAlignment.Top, Values, new KeypadView(VM.Keypad))
          .Padding(new Insets(LineGap - 1, 0, 0, 0))
      )
      .Frame(width: _width)
      // A chip just started being carried: the editor makes room for it.
      .OnChange(
        () => DragSession.Active,
        carrying =>
        {
          if (carrying)
            VM.BeganCarrying();
        }
      );
}
