using System.Text;
using Element.Application;
using Element.Domain.Combo;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Combo;

// The combo count and its bump, and the song title mirrored from ADOFAI's own
// level name, which stays hidden while the overlay is shown.
internal sealed class ComboValues : IOverlayView<ComboSnapshot>
{
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(16);
  private GameObject _songTitleRoot;
  private SongTitleMirror _songTitleMirror;
  private int _lastCount = -1;

  public ComboValues(OverlayCanvasHost host)
  {
    _values = host.Values;
    _songTitleRoot = new GameObject("Song title source");
    _songTitleRoot.transform.SetParent(host.Root.transform, false);
    _songTitleMirror = _songTitleRoot.AddComponent<SongTitleMirror>();
    _songTitleMirror.Initialize(title => _values.Set(OverlayValueId.SongTitle, title));
    _songTitleRoot.SetActive(false);
  }

  public void Show()
  {
    _songTitleRoot?.SetActive(true);
  }

  public void Hide()
  {
    _songTitleRoot?.SetActive(false);
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
    if (snapshot.Bump)
      _values.BumpCombo();
  }

  public void Dispose()
  {
    _songTitleMirror?.Release();
    if (_songTitleRoot != null)
      Object.Destroy(_songTitleRoot);
    _songTitleRoot = null;
    _songTitleMirror = null;
  }
}
