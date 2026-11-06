using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Element.Domain.Input;
using UnityEngine;

namespace Element.Configuration;

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
  /// <summary>The window before version 8, measured from the key's release.</summary>
  internal const int ReleaseWindowDefaultMs = 20;

  public bool Enabled = true;

  /// <summary>A press of a key no later than this after its last accepted press is chatter.</summary>
  public int WindowMs = ChatterFilter.DefaultWindowMs;

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
  public static bool IsIgnorable(KeyCode key) =>
    key != KeyCode.None && key < KeyCode.Mouse0 && (int)key < ChatterFilter.KeyCount;

  public void Normalize()
  {
    WindowMs = ChatterFilter.ClampWindow(WindowMs);
    IgnoredKeys ??= new();
    var seen = new HashSet<KeyCode>();
    IgnoredKeys.RemoveAll(key => !IsIgnorable(key) || !seen.Add(key));
  }
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
