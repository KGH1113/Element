using System;
using System.Collections.Generic;
using Element.Application.Rendering;
using UnityEngine;

namespace Element.Infrastructure.TufReplay;

/// <summary>
/// The object Element registers with TUFReplay's ReplayApi so its overlay and ElementNote's
/// key viewers appear in rendered replays. TUFReplay finds these public methods by name.
/// The overlay is a canvas the renderer captures; the key viewers are videos ElementNote
/// draws alongside the render, which the renderer lays over the finished video.
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

  // Once, after the last frame of a completed render; it may wait for ElementNote.
  public void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)
  {
    if (_rendering && add != null)
      _overlay.AddVideos(add);
  }
}
