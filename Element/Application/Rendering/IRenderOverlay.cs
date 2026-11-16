using System;
using System.Collections.Generic;
using UnityEngine;

namespace Element.Application.Rendering;

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

  /// <summary>
  /// After the last frame: hands over the videos to lay over the finished video, each as
  /// <c>add(path, source, target)</c> in pixels from the top left.
  /// </summary>
  void AddVideos(Action<string, RectInt, RectInt> add);
}
