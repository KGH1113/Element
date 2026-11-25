using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;

namespace Element.Presentation.Overlay.Text;

// A run with its formulas parsed once, when the text's settings change.
internal sealed class OverlayCompiledRun
{
  public OverlayTextRun Run;
  public OverlayFormula Value;
  public OverlayFormula Size;
  public OverlayFormula Alpha;

  // The run's OverlayFontKey in saved form; empty follows the default font.
  public string Font;

  // The run's slot in the text's OverlayTextGradients; -1 for a solid color.
  public int Gradient = -1;
}
