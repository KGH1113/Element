using System.Collections.Generic;

namespace Element.Infrastructure.TufReplay;

/// <summary>
/// Saved by TUFReplay with each run. Older runs are read back by member name and type, so
/// renaming a member or changing its type needs an <c>UpgradeRunState</c> method on the provider.
/// </summary>
internal sealed class KeyCountRunState
{
  /// <summary>ElementNote tab id → key slot label → presses.</summary>
  public Dictionary<string, Dictionary<string, int>> ByTab = new();
}
