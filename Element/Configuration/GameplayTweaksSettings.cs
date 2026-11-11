using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace Element.Configuration;

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

[CopiedInPlace]
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

[CopiedInPlace]
public sealed class ChatterBlockerOptions
{
  public const int MinimumWindowMs = 10;
  public const int MaximumWindowMs = 500;
  public const int DefaultWindowMs = 100;

  /// <summary>The keys the chatter filter tells apart; one past them can't be ignored.</summary>
  public const int KeyCount = 1024;

  public bool Enabled = true;

  /// <summary>A press of a key no later than this after its last accepted press is chatter.</summary>
  public int WindowMs = DefaultWindowMs;

  /// <summary>Keys never filtered, such as one used for fast jacks.</summary>
  public List<KeyCode> IgnoredKeys = new();

  public bool Ignores(KeyCode key) => IgnoredKeys != null && IgnoredKeys.Contains(key);

  /// <summary>Adds the key to the ignored keys, or takes it off when it is already there.</summary>
  public void ToggleIgnored(KeyCode key)
  {
    IgnoredKeys ??= new();
    if (!IgnoredKeys.Remove(key) && IsIgnorable(key))
      IgnoredKeys.Add(key);
  }

  /// <summary>Keyboard keys only, as KeyboardChatterBlocker lets you choose.</summary>
  public static bool IsIgnorable(KeyCode key) => key != KeyCode.None && key < KeyCode.Mouse0 && (int)key < KeyCount;

  public static int ClampWindow(int milliseconds) => Math.Max(MinimumWindowMs, Math.Min(MaximumWindowMs, milliseconds));

  public void Normalize()
  {
    WindowMs = ClampWindow(WindowMs);
    IgnoredKeys ??= new();
    var seen = new HashSet<KeyCode>();
    IgnoredKeys.RemoveAll(key => !IsIgnorable(key) || !seen.Add(key));
  }
}

[CopiedInPlace]
public sealed class JudgementVisualOptions
{
  /// <summary>1 once X-Perfect has its own switch; SettingsMigrator splits it from Perfect.</summary>
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
}

[CopiedInPlace]
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

[CopiedInPlace]
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

[CopiedInPlace]
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
