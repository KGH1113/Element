using ImplResourcePack.Application;
using ImplResourcePack.Domain.TimingScale;

namespace ImplResourcePack.Infrastructure.Game.TimingScale;

internal sealed class TimingScaleReader : ITimingScaleSource
{
  public TimingScaleSnapshot Read()
  {
    scrController controller = scrController.instance;
    if (controller == null || controller.currFloor == null)
      return default;

    return new TimingScaleSnapshot(controller.currFloor.marginScale);
  }
}
