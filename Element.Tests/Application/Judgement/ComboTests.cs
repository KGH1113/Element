using Element.Application.Judgement;
using Element.Application.Overlay;
using Element.Domain.Judgement;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Judgement;

internal static class ComboTests
{
  [Suite("midspin tiles leave the combo unchanged")]
  public static void Run()
  {
    MidspinNeitherExtendsNorBreaksTheCombo();
  }

  private static void MidspinNeitherExtendsNorBreaksTheCombo()
  {
    Check(!HitMarginPolicy.AffectsCombo(HitMargin.Midspin), "Midspin should not touch the combo");
    Check(HitMarginPolicy.AffectsCombo(HitMargin.XPerfect), "XPerfect should count toward the combo");

    RecordingView view = new();
    ComboFeature combo = new(view);
    combo.Show();
    combo.RegisterHit(HitMargin.XPerfect);
    combo.RegisterHit(HitMargin.PerfectPlus);
    int renders = view.Renders;

    combo.RegisterHit(HitMargin.Midspin);
    Equal(renders, view.Renders);
    Equal(2, view.Last.Count);

    combo.RegisterHit(HitMargin.XPerfect);
    Equal(3, view.Last.Count);

    combo.RegisterHit(HitMargin.EarlyPerfect);
    Equal(0, view.Last.Count);
  }

  private sealed class RecordingView : IOverlayView<ComboSnapshot>
  {
    public ComboSnapshot Last { get; private set; }
    public int Renders { get; private set; }

    public void Show() { }

    public void Hide() { }

    public void Render(ComboSnapshot snapshot)
    {
      Last = snapshot;
      Renders++;
    }

    public void Dispose() { }
  }
}
