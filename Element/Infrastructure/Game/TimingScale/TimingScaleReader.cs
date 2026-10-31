using Element.Application;
using Element.Domain.TimingScale;

namespace Element.Infrastructure.Game.TimingScale;

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
