using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace ImplResourcePack.Configuration.Effects;

[Flags]
public enum RemovedEffects : long
{
  None = 0,
  CameraMotion = 1L << 0,
  ScreenFlash = 1L << 1,
  ScreenShake = 1L << 2,
  Filters = 1L << 3,
  AdvancedFilters = 1L << 4,
  Backgrounds = 1L << 5,
  Decorations = 1L << 6,
  Particles = 1L << 7,
  TrackAnimations = 1L << 8,
  TrackMovement = 1L << 9,
  TrackPosition = 1L << 10,
  TrackColors = 1L << 11,
  TrackOpacity = 1L << 12,
  PlanetOrbit = 1L << 13,
  PlanetScale = 1L << 14,
  PlanetRadius = 1L << 15,
  HitSounds = 1L << 16,
  HoldSounds = 1L << 17,
  HideIcons = 1L << 18,
  RepeatEvents = 1L << 19,
  FrameRate = 1L << 20,
  Checkpoints = 1L << 21,
}

[Serializable]
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

  public static int ClampCameraZoom(int zoom) => Math.Max(MinimumCameraZoom, Math.Min(MaximumCameraZoom, zoom));

  public EffectPreset Copy() =>
    new()
    {
      Id = Id,
      Name = Name,
      Effects = Effects,
      CameraZoom = CameraZoom,
    };
}

[Serializable]
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
  public int ActiveCameraZoom => EffectPreset.ClampCameraZoom(ActivePreset?.CameraZoom ?? EffectPreset.DefaultCameraZoom);

  public void Normalize()
  {
    Presets ??= new List<EffectPreset>();
    MigrateDefaults();
    var ids = new HashSet<string>(StringComparer.Ordinal);
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
    }
    if (ActivePreset == null)
      ActivePresetId = Presets.Count > 0 ? Presets[0].Id : "";
    if (Presets.Count == 0)
      Enabled = false;
  }

  public bool TryAdd(string name, out EffectPreset preset, out string error)
  {
    preset = null;
    if (!ValidateName(name, null, out string normalized, out error))
      return false;
    preset = new EffectPreset { Name = normalized };
    Presets.Add(preset);
    if (ActivePreset == null)
      ActivePresetId = preset.Id;
    return true;
  }

  public bool TryRename(string id, string name, out string error)
  {
    EffectPreset preset = Presets.Find(p => p.Id == id);
    if (preset == null)
    {
      error = "This preset no longer exists.";
      return false;
    }
    if (!ValidateName(name, id, out string normalized, out error))
      return false;
    preset.Name = normalized;
    return true;
  }

  public bool Select(string id)
  {
    if (!Presets.Exists(p => p.Id == id))
      return false;
    ActivePresetId = id;
    return true;
  }

  public bool Remove(string id)
  {
    int index = Presets.FindIndex(p => p.Id == id);
    if (index < 0)
      return false;
    Presets.RemoveAt(index);
    if (ActivePresetId == id)
      ActivePresetId = Presets.Count > 0 ? Presets[Math.Min(index, Presets.Count - 1)].Id : "";
    if (Presets.Count == 0)
      Enabled = false;
    return true;
  }

  public bool ValidateName(string name, string exceptId, out string normalized, out string error)
  {
    normalized = (name ?? "").Trim();
    string candidate = normalized;
    error = "";
    if (normalized.Length == 0)
      error = "Give this preset a name.";
    else if (normalized.Length > MaximumNameLength)
      error = "Use 40 characters or fewer.";
    else if (
      Presets.Exists(p => p.Id != exceptId && string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase))
    )
      error = "A preset already uses this name. Choose another.";
    return error.Length == 0;
  }

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
