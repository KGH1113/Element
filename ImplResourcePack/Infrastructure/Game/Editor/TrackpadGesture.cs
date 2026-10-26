using System;
using System.Runtime.InteropServices;

namespace ImplResourcePack.Infrastructure.Game.Editor;

[StructLayout(LayoutKind.Sequential)]
internal struct TrackpadEvent
{
  public int Kind, Phase, Momentum, Reserved;
  public double X, Y, DeltaX, DeltaY, Magnification;
}

// Pure state/math, also exercised without a running Unity player.
internal sealed class TrackpadGesture
{
  private enum Owner { None, Canvas, Ui, Cancelled }
  private Owner _owner;
  private bool _cancelledCanvas;
  public bool BlocksUi => _owner == Owner.Canvas || (_owner == Owner.Cancelled && _cancelledCanvas);

  public void Reset()
  {
    _owner = Owner.None;
    _cancelledCanvas = false;
  }

  public void Cancel()
  {
    _cancelledCanvas |= _owner == Owner.Canvas;
    _owner = Owner.Cancelled;
  }

  public bool Route(int phase, int momentum, Func<bool> canStartOnCanvas)
  {
    if ((phase & 16) != 0 || (momentum & 16) != 0)
      Cancel();
    else if ((phase & 1) != 0 ||
      (momentum == 0 && phase == 0) ||
      (_owner == Owner.None && momentum == 0 && (phase & (8 | 32)) == 0))
    {
      _cancelledCanvas = false;
      _owner = canStartOnCanvas() ? Owner.Canvas : Owner.Ui;
    }

    // Retain the target after finger-up: momentum belongs to that same target.
    return _owner == Owner.Canvas;
  }

  public static double ZoomSize(double size, double magnification)
  {
    if (double.IsNaN(magnification) || double.IsInfinity(magnification) || magnification <= -1)
      return size;
    return Math.Max(0.5, Math.Min(15, size / (1 + magnification)));
  }
}
