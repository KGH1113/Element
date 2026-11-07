using System.Collections.Generic;

namespace Element.Application.Rendering;

/// <summary>
/// The ElementNote key counts a run was recorded at, while TUFReplay renders that run to video.
/// The video's key viewers count on from them instead of the player's current totals. Null
/// outside such a render and for runs recorded without them.
/// </summary>
internal sealed class RecordedKeyCounts
{
  /// <summary>ElementNote tab id → key slot label → presses.</summary>
  public Dictionary<string, Dictionary<string, int>> Counts { get; set; }
}
