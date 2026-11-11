using System;

namespace Element.Configuration.Gameplay;

[CopiedInPlace]
public sealed class GameplayTweaksSettings
{
  public EditorTweaksOptions Editor = new();
  public JudgementVisualOptions Judgement = new();
  public PlanetAppearanceOptions Planets = new();
  public ChatterBlockerOptions Chatter = new();

  public void Normalize()
  {
    Editor ??= new();
    Judgement ??= new();
    Planets ??= new();
    Chatter ??= new();
    Chatter.Normalize();
    Editor.RotationStep = FiniteClamp(Editor.RotationStep, 0.1f, 180f, 22.5f);
    Planets.Normalize();
  }

  internal static float FiniteClamp(float value, float minimum, float maximum, float fallback) =>
    float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));
}
