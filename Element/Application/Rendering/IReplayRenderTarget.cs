using System;

namespace Element.Application.Rendering;

/// <summary>The video TUFReplay renders, as its ReplayApi reports it. Unity main thread.</summary>
internal interface IReplayRenderTarget
{
  /// <summary>The video's size while TUFReplay renders a run, otherwise 0.</summary>
  int RenderWidth { get; }
  int RenderHeight { get; }

  /// <summary>The video's frames per second while TUFReplay renders a run, otherwise 0.</summary>
  int RenderFps { get; }

  /// <summary>The renderer lays providers' videos over the finished video.</summary>
  bool RenderTakesOverlayVideos { get; }

  /// <summary>The FFmpeg TUFReplay shares with the renderer while it renders a run, or null.</summary>
  string RenderFfmpegPath { get; }
}
