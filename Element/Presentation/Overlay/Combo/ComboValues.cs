using System.Text;
using Element.Application.Overlay;
using Element.Domain.Combo;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;

namespace Element.Presentation.Overlay.Combo;

// The combo count, and the song title mirrored from ADOFAI's own level name,
// which stays hidden while the overlay is shown.
internal sealed class ComboValues : IOverlayView<ComboSnapshot>
{
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(16);
  private ISongTitleMirror _songTitle;
  private int _lastCount = -1;

  public ComboValues(OverlayValues values, ISongTitleMirror songTitle)
  {
    _values = values;
    _songTitle = songTitle;
    _songTitle.Follow(
      (title, size) => _values.Set(OverlayValueId.SongTitle, SongTitleMarkupNormalizer.Normalize(title, size))
    );
  }

  public void Show()
  {
    _songTitle?.SetActive(true);
  }

  public void Hide()
  {
    _songTitle?.SetActive(false);
    _values.Clear(OverlayValueId.Combo);
    _values.Clear(OverlayValueId.SongTitle);
    _lastCount = -1;
  }

  public void Render(ComboSnapshot snapshot)
  {
    if (_lastCount != snapshot.Count)
    {
      _values.Set(OverlayValueId.Combo, _builder.Clear().AppendInt(snapshot.Count), snapshot.Count);
      _lastCount = snapshot.Count;
    }
  }

  public void Dispose()
  {
    _songTitle?.Release();
    _songTitle = null;
  }
}
