using System;

namespace Element.Domain.Attempts;

/// <summary>
/// Where a play started and how fast it ran. Like JipperResourcePack, attempts
/// count separately for each start tile and speed.
/// </summary>
internal readonly struct AttemptKey : IEquatable<AttemptKey>
{
  public AttemptKey(int startFloor, double speed)
  {
    StartFloor = startFloor;
    // Pitch × tile speed carries float noise; four decimals keep one key per speed.
    Speed = double.IsNaN(speed) || double.IsInfinity(speed) ? 0f : (float)Math.Round(speed, 4);
  }

  public int StartFloor { get; }

  /// <summary>Song pitch × the start tile's planet speed.</summary>
  public float Speed { get; }

  public bool Equals(AttemptKey other) => StartFloor == other.StartFloor && Speed.Equals(other.Speed);

  public override bool Equals(object obj) => obj is AttemptKey other && Equals(other);

  public override int GetHashCode() => unchecked(StartFloor * 397 ^ Speed.GetHashCode());
}
