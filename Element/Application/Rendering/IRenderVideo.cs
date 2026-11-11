namespace Element.Application.Rendering;

/// <summary>
/// A video being rendered offline, such as by OrbitRender. The screen then runs
/// slower than the video and plays the level from the editor, so values that
/// follow the screen follow the video instead.
/// </summary>
internal interface IRenderVideo
{
  bool IsRendering { get; }
}
