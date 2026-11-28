using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Dock.Line;
using Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;
using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// The left of the editor's lower row, under the line between the rows: the
/// line (the variable open and its pieces), then the values that fit there
/// (<see cref="ValueBrowserView"/>). As tall as the input method beside it
/// with the room it keeps over it. No card behind it. Reads only its view model.
/// </summary>
internal sealed class InputDockView : View<InputDockViewModel>
{
  /// <summary>Between the two columns of an editor.</summary>
  public const float ColumnGap = 28;

  /// <summary>Between the line across the rows and the input method under it.</summary>
  public const float MethodGap = 12;

  /// <summary>How tall the lower row is, from the line between the rows: the input method and the room over it.</summary>
  public const float Height = KeypadView.Height + MethodGap;

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

  public override View Body => VStack(spacing: 0, new FormulaLineView(VM.Line), Values).Frame(width: _width);
}
