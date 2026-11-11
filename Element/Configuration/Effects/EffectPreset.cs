using System;
using UnityEngine;

namespace Element.Configuration.Effects;

[Serializable]
[CopiedInPlace]
public sealed class EffectPreset
{
  public const int MinimumCameraZoom = 100;
  public const int MaximumCameraZoom = 1000;
  public const int DefaultCameraZoom = 250;

  public string Id = Guid.NewGuid().ToString("N");
  public string Name = "";
  public RemovedEffects Effects;

  /// <summary>Zoom percent of the camera that follows the planets once camera movement is removed.</summary>
  public int CameraZoom = DefaultCameraZoom;

  /// <summary>Applies this preset when pressed outside play; None when the preset has no shortcut.</summary>
  public KeyCode ShortcutKey = KeyCode.None;

  /// <summary>Held with <see cref="ShortcutKey"/>; None for a single key.</summary>
  public KeyCode ShortcutModifier = KeyCode.None;

  public bool HasShortcut => ShortcutKey != KeyCode.None;

  public bool UsesShortcut(KeyCode key, KeyCode modifier) =>
    HasShortcut && ShortcutKey == key && ShortcutModifier == modifier;

  public void ClearShortcut()
  {
    ShortcutKey = KeyCode.None;
    ShortcutModifier = KeyCode.None;
  }

  public static int ClampCameraZoom(int zoom) => Math.Max(MinimumCameraZoom, Math.Min(MaximumCameraZoom, zoom));

  public EffectPreset Copy() =>
    new()
    {
      Id = Id,
      Name = Name,
      Effects = Effects,
      CameraZoom = CameraZoom,
      ShortcutKey = ShortcutKey,
      ShortcutModifier = ShortcutModifier,
    };
}
