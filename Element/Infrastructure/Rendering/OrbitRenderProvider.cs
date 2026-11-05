using System.Collections.Generic;
using UnityEngine;

namespace Element.Infrastructure.Rendering;

/// <summary>What Element does for OrbitRender's renders. Unity main thread.</summary>
internal interface IRenderOverlay
{
  /// <summary>Read when a render starts, after <see cref="Begin"/>.</summary>
  IEnumerable<Canvas> Canvases();
  void Begin(bool replay);
  void End();

  /// <summary>Video time in 100 ns units.</summary>
  void Clock(long ticks);
  void Input(string key, bool down, long ticks);

  /// <summary>Runs right before OrbitRender draws the overlay canvases for a video frame.</summary>
  void BeforeCapture();
}

/// <summary>
/// The object Element registers with OrbitRender, which finds these public
/// methods by name. Their names and parameters are OrbitRender's overlay API:
/// API 2 sends recorded keys to <see cref="OnReplayKey"/>, API 1 to <see cref="OnReplayInput"/>.
/// </summary>
internal sealed class OrbitRenderProvider
{
  private readonly IRenderOverlay _overlay;

  public OrbitRenderProvider(IRenderOverlay overlay) => _overlay = overlay;

  public IEnumerable<Canvas> GetOverlayCanvases() => _overlay.Canvases();

  public void OnRenderSessionBegin(bool replay) => _overlay.Begin(replay);

  public void OnRenderSessionEnd() => _overlay.End();

  public void OnReplayClock(double videoSeconds, long ticks) => _overlay.Clock(ticks);

  /// <summary>API 2. OrbitRender calls this instead of <see cref="OnReplayInput"/> when it exists.</summary>
  public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)
  {
    // A key Unity cannot name arrives with only its native code, which means
    // nothing without the platform it was recorded on, and OrbitRender does not
    // send that platform yet. ElementNote would skip the key anyway.
    if (key != KeyCode.None)
      _overlay.Input(key.ToString(), down, ticks);
  }

  /// <summary>API 1, used by OrbitRender 2.7.0 and earlier.</summary>
  public void OnReplayInput(string key, bool down, long ticks) => _overlay.Input(key, down, ticks);

  public void OnBeforeOverlayCapture(bool replay) => _overlay.BeforeCapture();

  public void OnAfterOverlayCapture(bool replay) { }
}
