using System.Text;
using Element.Application;
using Element.Domain.Attempts;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.Attempt;

internal sealed class AttemptValues : IOverlayView<AttemptCounts>
{
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(16);

  public AttemptValues(OverlayValues values)
  {
    _values = values;
  }

  public void Show() { }

  public void Hide()
  {
    _values.Clear(OverlayValueId.Attempt);
    _values.Clear(OverlayValueId.FullAttempt);
  }

  public void Render(AttemptCounts counts)
  {
    _values.Set(OverlayValueId.Attempt, _builder.Clear().AppendInt(counts.Attempt), counts.Attempt);
    _values.Set(OverlayValueId.FullAttempt, _builder.Clear().AppendInt(counts.FullAttempt), counts.FullAttempt);
  }

  public void Dispose() { }
}
