namespace Tabub.Planning;

/// <summary>
/// Beats at one fixed BPM, so hits as far apart in time count the same
/// however the chart's speed changes. A half turn (180°) at the reference BPM
/// is one beat:
/// <code>beats = (angle / 180°) × (reference BPM / current BPM)</code>
/// which is the time between the hits × reference BPM / 60. The game already
/// works out each tile's time with twirls, speed changes and pauses in it, so
/// Tabub reads beats from those times.
/// </summary>
public static class TabubBeats
{
  public static double FromSeconds(double seconds, double referenceBpm) => seconds * referenceBpm / 60.0;

  public static double FromTurn(double degrees, double bpm, double referenceBpm) => degrees / 180.0 * (referenceBpm / bpm);
}
