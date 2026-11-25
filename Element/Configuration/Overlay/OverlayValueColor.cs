using System;
using System.Collections.Generic;

namespace Element.Configuration.Overlay;

// A color that followed a live value, as earlier versions saved it. Nothing
// draws from it any more; it is read only to convert old settings. Value is
// the key of the value it followed ("accuracy", or a mod's "Mod/key"), empty
// when none was picked. Each stop gave the color at one value; the lowest and
// highest stops held below and above them. The stops are kept in the order
// they were made, not by value.
[Serializable]
[CopiedInPlace]
public sealed class OverlayValueColor
{
  public const int MaximumStops = 8,
    MinimumStops = 2;

  public string Value = string.Empty;
  public OverlayValueColorMode Mode = OverlayValueColorMode.Smooth;
  public List<OverlayValueColorStop> Stops = new();

  public OverlayValueColor Clone()
  {
    OverlayValueColor copy = new() { Value = Value ?? string.Empty, Mode = Mode };
    if (Stops != null)
      foreach (OverlayValueColorStop stop in Stops)
        if (stop != null)
          copy.Stops.Add(stop.Clone());
    return copy;
  }

  public bool SameAs(OverlayValueColor other)
  {
    if (other == null || Mode != other.Mode || !OverlayColorSettings.SameFormula(Value, other.Value))
      return false;
    int count = Stops?.Count ?? 0;
    if (count != (other.Stops?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Stops[i]?.SameAs(other.Stops[i]) ?? other.Stops[i] == null))
        return false;
    return true;
  }

  /// <summary>Both off, or both following a value the same way.</summary>
  public static bool Same(OverlayValueColor a, OverlayValueColor b) => a == null ? b == null : a.SameAs(b);
}
