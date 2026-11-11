using Element.Application.Overlay;
using Element.Domain.Judgement;

namespace Element.Application.Judgement;

internal sealed class JudgementFeature : IOverlayFeature
{
  private readonly IJudgementSource _reader;
  private readonly IOverlayView<JudgementSnapshot> _view;

  public JudgementFeature(IJudgementSource reader, IOverlayView<JudgementSnapshot> view)
  {
    _reader = reader;
    _view = view;
  }

  public void Show()
  {
    _view.Show();
    Refresh();
  }

  public void Hide()
  {
    _view.Hide();
  }

  public void Refresh()
  {
    if (_reader.TryRead(out JudgementSnapshot snapshot))
      _view.Render(snapshot);
  }

  /// <summary>Shows the counts the game just changed, read where they changed.</summary>
  public void Refresh(JudgementSnapshot counts) => _view.Render(counts);

  public void Dispose()
  {
    _view.Dispose();
  }
}
