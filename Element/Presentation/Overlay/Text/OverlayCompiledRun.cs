using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;

namespace Element.Presentation.Overlay.Text;

// A run with its formulas parsed once, when the text's settings change.
internal sealed class OverlayCompiledRun
{
  public OverlayTextRun Run;
  public OverlayVariable Value;
  public OverlayVariable Size;
  public OverlayVariable Alpha;

  // The run's OverlayFontKey in saved form; empty follows the default font.
  public string Font;

  // The run's slot in the text's OverlayTextGradients; -1 for a solid color.
  public int Gradient = -1;

  // Cases that give the run another color while they hold; null for none.
  public OverlayVariable Cases;
}
