using System;

namespace Element.Application.Rendering;

/// <summary>The ElementNote that draws key viewers for renders, as a render sees it.</summary>
internal interface IKeyViewerRenderApp : IKeyViewerRenderChannel
{
  /// <summary>An ElementNote that speaks Element's render protocol is connected.</summary>
  bool Available { get; }

  /// <summary>It can start a render's key counts from a run's recorded counts.</summary>
  bool TakesRecordedCounts { get; }

  /// <summary>Its answers to begin and finish.</summary>
  RenderReplies Replies { get; }
}
