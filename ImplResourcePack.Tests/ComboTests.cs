using System;
using ImplResourcePack.Application;
using ImplResourcePack.Application.Features;
using ImplResourcePack.Domain.Combo;
using ImplResourcePack.Domain.Judgement;

namespace ImplResourcePack.Tests;

internal static class ComboTests
{
  public static void Run()
  {
    MidspinNeitherExtendsNorBreaksTheCombo();
  }

  private static void MidspinNeitherExtendsNorBreaksTheCombo()
  {
    True(!HitMarginPolicy.AffectsCombo(HitMargin.Midspin), "Midspin should not touch the combo");
    True(HitMarginPolicy.AffectsCombo(HitMargin.XPerfect), "XPerfect should count toward the combo");

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

  private static void True(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private static void Equal<T>(T expected, T actual)
  {
    if (!Equals(expected, actual))
      throw new InvalidOperationException("Expected " + expected + ", got " + actual);
  }
}
