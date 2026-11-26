using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The left column's lower part, under a hairline that ends the preview: the
/// line (the variable open and its pieces), then the values that fit there
/// (<see cref="ValueBrowserView"/>). As tall as the input method beside it,
/// in the right column. No card behind it. Reads only its view model.
/// </summary>
internal sealed class InputDockView : View<InputDockViewModel>
{
  /// <summary>Between the two columns of an editor.</summary>
  public const float ColumnGap = 28;

  /// <summary>How tall the dock is: the hairline, the line and the values.</summary>
  public const float Height = KeypadView.Height;

  private readonly float _width;

  /// <param name="width">The left column's width.</param>
  public InputDockView(InputDockViewModel vm, float width)
    : base(vm)
  {
    _width = width;
    Values = new ValueBrowserView(vm.Values);
  }

  /// <summary>The values under the line.</summary>
  public ValueBrowserView Values { get; }

  public override View Body =>
    VStack(spacing: 0, HairLine.Across(), new FormulaLineView(VM.Line), Values).Frame(width: _width);
}
