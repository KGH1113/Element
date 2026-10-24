using System.Collections.Generic;
using ImplResourcePack.Configuration;

namespace ImplResourcePack.Domain.Layout;

internal static class OverlayLayoutDefaults
{
  public static void Restore(IEnumerable<OverlayLayoutSettings> layouts, HashSet<string> defaultIds)
  {
    foreach (OverlayLayoutSettings layout in layouts)
    {
      if (layout == null)
        continue;
      // Retain disabled instances so undo/cancel can recover the exact objects.
      layout.Enabled = defaultIds.Contains(layout.Id);
      layout.Customized = false;
      layout.LayoutVersion = 0;
      layout.Anchor = Anchor9.Center;
      layout.OffsetX = layout.OffsetY = 0;
      layout.CenterX = layout.CenterY = .5f;
      layout.Scale = 1;
    }
  }
}
