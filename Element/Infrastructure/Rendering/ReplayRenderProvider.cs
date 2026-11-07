using System.Collections.Generic;
using UnityEngine;

namespace Element.Infrastructure.Rendering;

/// <summary>What Element does while TUFReplay renders a run to video. Unity main thread.</summary>
internal interface IRenderOverlay
{
  /// <summary>Read while the video is captured, after <see cref="Begin"/>.</summary>
  IEnumerable<Canvas> Canvases();
  void Begin();
  void End();

  /// <summary>Video time in 100 ns units.</summary>
  void Clock(long ticks);
  void Input(string key, bool down, long ticks);

  /// <summary>Runs right before a video frame is captured.</summary>
  void BeforeCapture();
}

/// <summary>
/// The object Element registers with TUFReplay's ReplayApi so its overlay and ElementNote's
/// key viewers appear in rendered replays. TUFReplay finds these public methods by name.
/// Replays played in the game are left alone: ElementNote shows them in its own window.
/// </summary>
internal sealed class ReplayRenderProvider
{
  public const string Id = "Element.Render";

  private readonly IRenderOverlay _overlay;
  private bool _rendering;

  public ReplayRenderProvider(IRenderOverlay overlay) => _overlay = overlay;

  public void OnReplayBegin(bool render)
  {
    if (_rendering)
      _overlay.End();
    _rendering = render;
    if (render)
      _overlay.Begin();
  }

  public void OnReplayEnd()
  {
    if (!_rendering)
      return;
    _rendering = false;
    _overlay.End();
  }

  public void OnReplayClock(long ticks)
  {
    if (_rendering)
      _overlay.Clock(ticks);
  }

  public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)
  {
    // A key Unity cannot name means nothing to ElementNote without the platform
    // it was recorded on, so ElementNote would skip it anyway.
    if (_rendering && key != KeyCode.None)
      _overlay.Input(key.ToString(), down, ticks);
  }

  // TUFReplay asks for these only while it renders.
  public IEnumerable<Canvas> GetReplayCanvases() => _overlay.Canvases();

  public void OnBeforeFrameCapture() => _overlay.BeforeCapture();
}
