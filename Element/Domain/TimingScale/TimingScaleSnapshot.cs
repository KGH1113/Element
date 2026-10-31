namespace Element.Domain.TimingScale;

internal readonly struct TimingScaleSnapshot
{
  public bool IsValid { get; }
  public double Scale { get; }

  public TimingScaleSnapshot(double scale)
  {
    IsValid = !double.IsNaN(scale) && !double.IsInfinity(scale);
    Scale = scale;
  }
}
