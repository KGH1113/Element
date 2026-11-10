using Element.Application;
using UnityEngine;

namespace Element.Infrastructure.Rendering;

/// <summary>
/// A video rendered offline, as OrbitRender does for normal renders and for TUFReplay's runs:
/// the game advances a fixed step per video frame (<see cref="Time.captureFramerate"/>).
/// </summary>
internal sealed class OfflineRender : IRenderVideo
{
  public bool IsRendering => Time.captureFramerate > 0;
}
