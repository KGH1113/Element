using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

namespace Element.Configuration.Effects;

[Serializable]
[CopiedInPlace]
public sealed class EffectSettings
{
  public const int MaximumNameLength = 40;
  public const int CurrentDefaultsVersion = 1;
  public const string RemoveAllId = "remove-all";
  public const string NoFiltersId = "no-filters";
  public const string OnlyTilesId = "only-tiles";
  public int DefaultsVersion;
  public bool Enabled;
  public string ActivePresetId = RemoveAllId;

  [XmlIgnore]
  public List<EffectPreset> Presets = CreateDefaults();

  // XmlSerializer appends to constructor-initialized lists. Replace via an array
  // property so saved presets do not accumulate another set of defaults on load.
  [XmlArray("Presets")]
  public EffectPreset[] SerializedPresets
  {
    get => Presets?.ToArray() ?? Array.Empty<EffectPreset>();
    set => Presets = value == null ? new List<EffectPreset>() : new List<EffectPreset>(value);
  }

  [XmlIgnore]
  public EffectPreset ActivePreset => Presets?.Find(p => p != null && p.Id == ActivePresetId);

  [XmlIgnore]
  public RemovedEffects ActiveEffects => Enabled ? ActivePreset?.Effects ?? RemovedEffects.None : RemovedEffects.None;

  [XmlIgnore]
  public int ActiveCameraZoom =>
    EffectPreset.ClampCameraZoom(ActivePreset?.CameraZoom ?? EffectPreset.DefaultCameraZoom);

  public void Normalize()
  {
    Presets ??= new List<EffectPreset>();
    MigrateDefaults();
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var shortcuts = new HashSet<(KeyCode, KeyCode)>();
    for (int i = Presets.Count - 1; i >= 0; i--)
      if (Presets[i] == null)
        Presets.RemoveAt(i);
    foreach (EffectPreset preset in Presets)
    {
      if (string.IsNullOrEmpty(preset.Id) || !ids.Add(preset.Id))
      {
        preset.Id = Guid.NewGuid().ToString("N");
        ids.Add(preset.Id);
      }
      string name = (preset.Name ?? "").Trim();
      if (name.Length == 0)
        name = "Preset";
      if (name.Length > MaximumNameLength)
        name = name.Substring(0, MaximumNameLength);
      string candidate = name;
      int suffix = 2;
      while (!names.Add(candidate))
      {
        string number = " " + suffix++;
        candidate = name.Substring(0, Math.Min(name.Length, MaximumNameLength - number.Length)) + number;
      }
      preset.Name = candidate;
      preset.Effects &= EffectCatalog.All;
      preset.CameraZoom = EffectPreset.ClampCameraZoom(preset.CameraZoom);
      // One shortcut applies one preset; the earlier preset keeps a duplicate.
      if (
        !KeyShortcut.IsValid(preset.ShortcutKey, preset.ShortcutModifier)
        || !shortcuts.Add((preset.ShortcutKey, preset.ShortcutModifier))
      )
        preset.ClearShortcut();
    }
    if (ActivePreset == null)
      ActivePresetId = Presets.Count > 0 ? Presets[0].Id : "";
    if (Presets.Count == 0)
      Enabled = false;
  }

  public bool Select(string id)
  {
    if (!Presets.Exists(p => p.Id == id))
      return false;
    ActivePresetId = id;
    return true;
  }

  /// <summary>Selects the preset and turns effect removal on, as clicking its card does.</summary>
  public bool Activate(string id)
  {
    if (!Select(id))
      return false;
    Enabled = true;
    return true;
  }

  /// <summary>
  /// Turns effect removal off when this preset is the one on; otherwise activates
  /// it. A preset's shortcut does this, so pressing it again undoes it.
  /// </summary>
  public bool Toggle(string id)
  {
    if (!Enabled || ActivePresetId != id)
      return Activate(id);
    Enabled = false;
    return true;
  }

  /// <summary>The preset other than <paramref name="exceptId"/> that this shortcut applies, or null.</summary>
  public EffectPreset FindShortcut(KeyCode key, KeyCode modifier, string exceptId = null) =>
    Presets.Find(p => p != null && p.Id != exceptId && p.UsesShortcut(key, modifier));

  private static List<EffectPreset> CreateDefaults()
  {
    return new List<EffectPreset>
    {
      new()
      {
        Id = RemoveAllId,
        Name = "Remove All",
        Effects = EffectCatalog.All,
      },
      new()
      {
        Id = NoFiltersId,
        Name = "No Filters",
        Effects = EffectCatalog.FilterEffects,
      },
      new()
      {
        Id = OnlyTilesId,
        Name = "Only Tiles",
        Effects = EffectCatalog.All & ~(EffectCatalog.TrackEffects | RemovedEffects.RepeatEvents),
      },
    };
  }

  private void MigrateDefaults()
  {
    if (DefaultsVersion >= CurrentDefaultsVersion)
      return;

    // Match the complete original definition. Renamed/edited presets and
    // intentionally deleted defaults are user choices, even when IDs are old.
    List<EffectPreset> replacements = CreateDefaults();
    foreach (EffectPreset preset in Presets)
    {
      int legacyIndex = UntouchedLegacyIndex(preset);
      if (legacyIndex < 0)
        continue;
      EffectPreset replacement = replacements[legacyIndex];
      string previousId = preset.Id;
      string id = replacement.Id;
      if (Presets.Exists(p => p != null && !ReferenceEquals(p, preset) && p.Id == id))
        id = Guid.NewGuid().ToString("N");
      string name = replacement.Name;
      int suffix = 2;
      while (
        Presets.Exists(p =>
          p != null
          && !ReferenceEquals(p, preset)
          && string.Equals(p.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)
        )
      )
        name = replacement.Name + " " + suffix++;
      preset.Id = id;
      preset.Name = name;
      preset.Effects = replacement.Effects;
      if (ActivePresetId == previousId)
        ActivePresetId = id;
    }
    DefaultsVersion = CurrentDefaultsVersion;
  }

  private static int UntouchedLegacyIndex(EffectPreset preset)
  {
    if (preset == null)
      return -1;
    const RemovedEffects calm =
      RemovedEffects.ScreenFlash | RemovedEffects.ScreenShake | RemovedEffects.Filters | RemovedEffects.AdvancedFilters;
    const RemovedEffects focus =
      calm
      | RemovedEffects.CameraMotion
      | RemovedEffects.Backgrounds
      | RemovedEffects.Decorations
      | RemovedEffects.Particles;
    const RemovedEffects minimal =
      focus
      | RemovedEffects.TrackAnimations
      | RemovedEffects.TrackMovement
      | RemovedEffects.TrackColors
      | RemovedEffects.TrackOpacity
      | RemovedEffects.PlanetScale
      | RemovedEffects.FrameRate;
    if (preset.Id == "focus" && preset.Name == "Focus" && preset.Effects == focus)
      return 0;
    if (preset.Id == "calm" && preset.Name == "Calm" && preset.Effects == calm)
      return 1;
    if (preset.Id == "minimal" && preset.Name == "Minimal" && preset.Effects == minimal)
      return 2;
    return -1;
  }
}
