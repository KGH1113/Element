using System;
using System.Runtime.InteropServices;

namespace Element.Infrastructure.Game.Editor;

[StructLayout(LayoutKind.Sequential)]
internal struct TrackpadEvent
{
  public int Kind,
    Phase,
    Momentum,
    Reserved;
  public double X,
    Y,
    DeltaX,
    DeltaY,
    Magnification;
}
