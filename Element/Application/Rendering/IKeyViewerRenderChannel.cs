using System;

namespace Element.Application.Rendering;

/// <summary>
/// Sends Element's render commands to the ElementNote that draws key viewers for
/// renders. Any thread: the render lease is kept from a timer.
/// </summary>
internal interface IKeyViewerRenderChannel
{
  /// <summary>The command's id for matching its reply, or null when no ElementNote takes renders.</summary>
  string Send(string name, object payload);
}
