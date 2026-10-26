using ImplResourcePack.Application;
using ImplResourcePack.Domain.Bpm;

namespace ImplResourcePack.Infrastructure.Game.Bpm;

internal sealed class BpmReader : IBpmSource
{
  public BpmSnapshot Read()
  {
    scrController controller = scrController.instance;
    scrConductor conductor = scrConductor.instance;
    if (controller == null || conductor == null || conductor.song == null)
      return BpmSnapshot.Empty;

    scrFloor floor = controller.currFloor ?? controller.firstFloor;
    if (floor == null)
      return BpmSnapshot.Empty;

    double? nextEntryTime = floor.nextfloor == null ? null : floor.nextfloor.entryTime;
    double planetSpeed = controller.playerOne.planetarySystem.speed;
    return BpmCalculator.Calculate(conductor.bpm, conductor.song.pitch, planetSpeed, floor.entryTime, nextEntryTime);
  }
}
