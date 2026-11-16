using System;
using Element.Application.Gameplay;
using Element.Configuration.Gameplay;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>
/// The planet preview of the Planets page: made the first time the page draws
/// a planet and let go when the window closes. After one failure it stays off
/// until the game restarts, and the page says the preview is unavailable.
/// </summary>
internal sealed class PlanetPreviews : IDisposable
{
  private readonly Func<IPlanetPreview> _create;
  private readonly Action<string, Exception> _logException;
  private IPlanetPreview _preview;
  private bool _failed;

  public PlanetPreviews(Func<IPlanetPreview> create, Action<string, Exception> logException)
  {
    _create = create;
    _logException = logException;
  }

  /// <summary>Planet <paramref name="index"/> in <paramref name="profile"/>'s colors; null when unavailable.</summary>
  public Texture Render(int index, PlanetAppearanceProfile profile)
  {
    if (_failed)
      return null;
    try
    {
      _preview ??= _create();
      return _preview.Render(index, profile);
    }
    catch (Exception exception)
    {
      _failed = true;
      Release();
      _logException("Render planet preview", exception);
      return null;
    }
  }

  /// <summary>
  /// Lets the preview target and its cloned planet visuals go; they are only
  /// needed while the window is open, and the page makes them again.
  /// </summary>
  public void Release()
  {
    _preview?.Dispose();
    _preview = null;
  }

  public void Dispose() => Release();
}
