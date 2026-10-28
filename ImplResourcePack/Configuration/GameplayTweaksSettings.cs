using System;
using System.Xml.Serialization;
using ImplResourcePack.Domain.Input;

namespace ImplResourcePack.Configuration;

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
    Chatter.WindowMs = ChatterFilter.ClampWindow(Chatter.WindowMs);
    Editor.RotationStep = FiniteClamp(Editor.RotationStep, 0.1f, 180f, 22.5f);
    Judgement.Normalize();
    Planets.Normalize();
  }

  internal static float FiniteClamp(float value, float minimum, float maximum, float fallback) =>
    float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));
}

public sealed class EditorTweaksOptions
{
  public bool ShowAngle;
  public bool ShowBeats;
  public bool ShowCount;
  public bool ShowDuration;
  public bool ExcludeLastInterval;
  public bool FineRotation;
  public float RotationStep = 22.5f;
  public bool SyncEventPositions;

  [XmlIgnore]
  public bool HasSelectionInfo => ShowAngle || ShowBeats || ShowCount || ShowDuration;
}

public sealed class ChatterBlockerOptions
{
  public bool Enabled;

  /// <summary>A press this soon after the same key's release is switch bounce.</summary>
  public int WindowMs = ChatterFilter.DefaultWindowMs;
}

public sealed class JudgementVisualOptions
{
  public int Version;
  public bool HidePerfect = true;
  public bool HideXPerfect = true;
  public bool HideEarlyPerfect;
  public bool HideLatePerfect;
  public bool HideTooEarly;
  public bool HideEarly;
  public bool HideLate;
  public bool HideTooLate;
  public bool HideMiss;
  public bool HideOverload;

  public void Normalize()
  {
    if (Version >= 1)
      return;
    // Previously PerfectMinus, XPerfect and PerfectPlus shared HidePerfect.
    // Keep that visibility on upgrade, then let the two controls vary separately.
    HideXPerfect = HidePerfect;
    Version = 1;
  }
}

public sealed class PlanetAppearanceOptions
{
  public bool Enabled;
  public PlanetAppearanceProfile Red = new("#FF3333");
  public PlanetAppearanceProfile Blue = new("#3380FF");
  public PlanetAppearanceProfile Green = new("#33CC66");

  public void Normalize()
  {
    Red ??= new("#FF3333");
    Blue ??= new("#3380FF");
    Green ??= new("#33CC66");
    Red.Normalize("#FF3333");
    Blue.Normalize("#3380FF");
    Green.Normalize("#33CC66");
  }
}

public sealed class PlanetAppearanceProfile
{
  public bool Enabled = true;
  public PlanetComponentAppearance Body = new();
  public PlanetComponentAppearance Tail = new();
  public PlanetComponentAppearance Ring = new();

  public PlanetAppearanceProfile() { }

  public PlanetAppearanceProfile(string color)
  {
    Body.ColorHex = Tail.ColorHex = Ring.ColorHex = color;
  }

  public void Normalize(string fallback)
  {
    Body ??= new();
    Tail ??= new();
    Ring ??= new();
    Body.Normalize(fallback);
    Tail.Normalize(fallback);
    Ring.Normalize(fallback);
  }
}

public sealed class PlanetComponentAppearance
{
  public bool OverrideColor = true;
  public string ColorHex = "#FFFFFF";
  public float Opacity = 100;

  public void Normalize(string fallback)
  {
    Opacity = GameplayTweaksSettings.FiniteClamp(Opacity, 0, 100, 100);
    string hex = ColorHex?.Trim().TrimStart('#');
    if (hex == null || hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _))
      ColorHex = fallback;
    else
      ColorHex = "#" + hex.ToUpperInvariant();
  }
}
