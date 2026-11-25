using System;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The input dock, under a hairline across the editor: the line (what is
/// open, its pieces laid out), under it for a few seconds the editor's
/// notice, or why the open field can't be finished as it is, then the values
/// on the left (<see cref="ValueBrowserView"/>) and the input method on the
/// right. No card behind it. Reads only its view model.
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
    Values = new ValueBrowserView(vm.Values);
  }

  /// <summary>The values beside the keypad.</summary>
  public ValueBrowserView Values { get; }

  public override View Body =>
    VStack(
        spacing: 0,
        HairLine.Across(),
        new FormulaLineView(VM.Line),
        // A notice for a few seconds; otherwise why the field can't be finished as it is.
        UI.Text(() => VM.Notices.Line.Value.Length > 0 ? VM.Notices.Line.Value : VM.Problem.Value)
          .Font(Font.Caption.Sized(13))
          .Foreground(() => VM.Notices.Line.Value.Length > 0 ? VM.Notices.Ink.Value : VM.Warn)
          .LineLimit(1)
          .Named("Notice")
          .Frame(maxWidth: float.PositiveInfinity, height: NoticeHeight, alignment: Alignment.TopLeading),
        HStack(spacing: KeypadGap, alignment: VAlignment.Top, Values, new InputMethodView(VM))
          .Padding(new Insets(LineGap - 1, 0, 0, 0))
      )
      .Frame(width: _width);
}
