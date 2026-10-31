using System;
using Element.Application.Attempts;
using Element.Domain.Attempts;

namespace Element.Infrastructure.Game.Attempts;

internal sealed class AttemptRunReader : IAttemptRunSource, IDisposable
{
  private readonly MapFingerprint _fingerprint = new();

  public bool TryRead(int startFloor, out AttemptRun run)
  {
    run = default;
    scrController controller = scrController.instance;
    scrConductor conductor = scrConductor.instance;
    if (controller == null || conductor == null || conductor.song == null)
      return false;
    scrPlayer player = controller.playerOne;
    if (player == null || player.planetarySystem == null)
      return false;
    if (!_fingerprint.TryCompute(out MapId map))
      return false;

    // Play sets every planet to the start tile's speed before this runs.
    double speed = conductor.song.pitch * player.planetarySystem.speed;
    run = new AttemptRun(map, new AttemptKey(startFloor, speed), !RDC.auto && !controller.noFail);
    return true;
  }

  public void Dispose() => _fingerprint.Dispose();
}
