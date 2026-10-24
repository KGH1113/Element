using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ADOFAI;
using ImplResourcePack.Configuration.Effects;
using ImplResourcePack.Infrastructure.Game.Effects;

namespace ImplResourcePack.Tests;

internal static class EffectSettingsTests
{
  public static void Run()
  {
    DefaultPresets();
    DefaultMigration();
    NamesAndSelection();
    PersistenceAndRepair();
    FilteringAndPlaybackBoundary();
  }

  private static void NamesAndSelection()
  {
    var settings = new EffectSettings();
    Check(!settings.Enabled && settings.ActiveEffects == RemovedEffects.None, "Effects must be opt-in.");
    Check(!settings.TryAdd(" remove all ", out _, out _), "Names must be case-insensitively unique after trimming.");
    Check(!settings.TryAdd(" \t ", out _, out _), "Blank names must be rejected.");
    Check(!settings.TryAdd(new string('A', 41), out _, out _), "Overlong names must be rejected.");
    Check(settings.TryAdd("  My quiet map  ", out EffectPreset added, out _), "A valid name must be accepted.");
    Check(added.Name == "My quiet map", "Names must be trimmed before persistence.");
    added.Effects = RemovedEffects.Backgrounds | RemovedEffects.ScreenFlash;
    Check(settings.Select(added.Id), "The new preset must be selectable.");
    settings.Enabled = true;
    Check(settings.ActiveEffects == added.Effects, "Only the selected preset controls removal.");
    Check(settings.TryRename(added.Id, " MY QUIET MAP ", out _), "A case-only self rename is valid.");
    Check(!settings.TryRename(added.Id, "No Filters", out _), "Renaming must not collide with a sibling.");
    Check(
      !settings.Select("missing") && settings.ActivePresetId == added.Id,
      "Invalid selection must preserve the active preset."
    );
    Check(
      settings.Remove(added.Id) && settings.ActivePreset != null,
      "Deleting the active preset must select a remaining preset."
    );
    while (settings.Presets.Count > 0)
      settings.Remove(settings.Presets[0].Id);
    Check(!settings.Enabled && settings.ActivePreset == null, "Deleting the final preset must disable removal.");
    settings.Normalize();
    Check(settings.Presets.Count == 0, "An intentionally empty gallery must survive normalization.");
  }

  private static void PersistenceAndRepair()
  {
    var original = new EffectSettings { Enabled = true };
    original.Select(EffectSettings.NoFiltersId);
    original.ActivePreset.Effects |= RemovedEffects.FrameRate;
    var serializer = new XmlSerializer(typeof(EffectSettings));
    using var text = new StringWriter();
    serializer.Serialize(text, original);
    using var input = new StringReader(text.ToString());
    var restored = (EffectSettings)serializer.Deserialize(input);
    restored.Normalize();
    Check(
      restored.Enabled && restored.ActivePresetId == EffectSettings.NoFiltersId,
      "Enabled state and active identity must survive XML persistence."
    );
    Check(restored.ActiveEffects == original.ActiveEffects, "Every selected effect must survive XML persistence.");
    Check(restored.Presets.Count == 3, "Loading a populated collection must not duplicate built-in presets.");

    var damaged = new EffectSettings
    {
      Enabled = true,
      ActivePresetId = "missing",
      Presets = new List<EffectPreset>
      {
        new() { Id = "same", Name = " name " },
        null,
        new()
        {
          Id = "same",
          Name = "NAME",
          Effects = (RemovedEffects)long.MaxValue,
        },
        new() { Id = "", Name = "" },
      },
    };
    damaged.Normalize();
    Check(
      damaged.Presets.Count == 3 && damaged.ActivePreset != null,
      "Corrupt entries must be repaired without losing valid presets."
    );
    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var ids = new HashSet<string>();
    foreach (EffectPreset preset in damaged.Presets)
    {
      Check(names.Add(preset.Name) && ids.Add(preset.Id), "Normalization must produce unique names and identities.");
      Check((preset.Effects & ~EffectCatalog.All) == 0, "Unknown persisted bits must be removed.");
    }
  }

  private static void FilteringAndPlaybackBoundary()
  {
    Check(
      !EffectsController.AllowsRendering(true, false, false),
      "Editing must keep every original effect and decoration available."
    );
    Check(
      EffectsController.AllowsRendering(true, true, false),
      "Starting editor test play must activate the preset before preparing visuals."
    );
    Check(EffectsController.AllowsRendering(false, false, false), "Normal gameplay must use the active preset.");
    Check(
      !EffectsController.AllowsRendering(false, true, true),
      "Returning to the editor must restore originals even inside a playback transition."
    );
    foreach (LevelEventType type in Enum.GetValues(typeof(LevelEventType)))
      Check(
        !EffectLevelFilter.RemovesEvent(type, RemovedEffects.None),
        "Disabled removal must pass every event through."
      );
    foreach (
      LevelEventType type in new[]
      {
        LevelEventType.SetSpeed,
        LevelEventType.Twirl,
        LevelEventType.Hold,
        LevelEventType.Pause,
        LevelEventType.Multitap,
        LevelEventType.MultiPlanet,
        LevelEventType.SetConditionalEvents,
        LevelEventType.SetInputEvent,
      }
    )
      Check(
        !EffectLevelFilter.RemovesEvent(type, EffectCatalog.All),
        "Removal must preserve gameplay timing/input: " + type
      );
    Check(
      EffectLevelFilter.RemovesEvent(LevelEventType.MoveCamera, RemovedEffects.CameraMotion),
      "Camera motion must be filterable."
    );
    Check(
      !EffectLevelFilter.RemovesEvent(LevelEventType.Flash, RemovedEffects.Filters),
      "Flashes must remain independently configurable."
    );
    Check(
      EffectLevelFilter.RemovesEvent(LevelEventType.Flash, RemovedEffects.ScreenFlash),
      "Flash filtering must be independent."
    );
    Check(
      EffectLevelFilter.RemovesEvent(LevelEventType.AddParticle, RemovedEffects.Particles),
      "Particle emitters must be removed with particle events."
    );
    Check(
      EffectCatalog.Count(EffectCatalog.All) == EffectCatalog.Entries.Length,
      "The catalog must expose every removable effect."
    );

    var settings = new EffectSettings { Enabled = true };
    using var controller = new EffectsController(() => settings);
    RemovedEffects initial = EffectsController.Applied;
    settings.Select(EffectSettings.NoFiltersId);
    controller.ApplySettings();
    Check(
      controller.RequiresRestart && EffectsController.Applied == initial,
      "Changing presets must not rebuild a running level."
    );
    Check(
      controller.CommitForPlayback() && !controller.RequiresRestart,
      "A playback boundary must apply the pending preset."
    );
    Check(EffectsController.Applied == settings.ActiveEffects, "The playback snapshot must match the selected preset.");
    settings.Enabled = false;
    controller.ApplySettings();
    Check(controller.RequiresRestart, "Disabling must schedule restoration at the next playback boundary.");
    controller.CommitForPlayback();
    Check(EffectsController.Applied == RemovedEffects.None, "Disabling must restore vanilla event processing.");
    controller.Dispose();
    Check(EffectsController.Active == null, "Disposal must release static patch ownership.");
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }

  private static void DefaultPresets()
  {
    var settings = new EffectSettings();
    settings.Normalize();
    Check(
      settings.Presets.Count == 3 && settings.ActivePresetId == EffectSettings.RemoveAllId,
      "New installations must start with exactly the requested three presets and Remove All selected."
    );
    Check(
      settings.Presets[0].Name == "Remove All"
        && settings.Presets[1].Name == "No Filters"
        && settings.Presets[2].Name == "Only Tiles",
      "Default names and gallery order must be stable."
    );
    RemovedEffects all = settings.Presets[0].Effects;
    RemovedEffects noFilters = settings.Presets[1].Effects;
    RemovedEffects tiles = settings.Presets[2].Effects;
    Check(all == EffectCatalog.All, "Remove All must enable every supported removal option.");
    Check(
      noFilters == (RemovedEffects.Filters | RemovedEffects.AdvancedFilters),
      "No Filters must affect only standard and advanced filters."
    );
    foreach (
      LevelEventType type in new[]
      {
        LevelEventType.SetFilter,
        LevelEventType.SetFilterAdvanced,
        LevelEventType.Bloom,
        LevelEventType.HallOfMirrors,
        LevelEventType.ScreenTile,
        LevelEventType.ScreenScroll,
      }
    )
      Check(EffectLevelFilter.RemovesEvent(type, noFilters), "No Filters must remove " + type);
    Check((noFilters & RemovedEffects.Decorations) == 0, "No Filters must preserve decoration definitions and events.");
    foreach (
      LevelEventType type in new[]
      {
        LevelEventType.Flash,
        LevelEventType.ShakeScreen,
        LevelEventType.MoveCamera,
        LevelEventType.AddParticle,
        LevelEventType.MoveTrack,
        LevelEventType.RepeatEvents,
      }
    )
      Check(!EffectLevelFilter.RemovesEvent(type, noFilters), "No Filters must preserve non-filter event " + type);

    Check(
      EffectCatalog.Count(EffectCatalog.TrackEffects) == 5,
      "Every track-removal option belongs to the preserved track mask."
    );
    foreach (EffectDefinition definition in EffectCatalog.Entries)
      Check(
        ((EffectCatalog.TrackEffects & definition.Flag) != 0) == (definition.Group == EffectGroup.Track),
        "The preserved track mask must match the Track editor group: " + definition.Name
      );
    Check(
      tiles == (EffectCatalog.All & ~(EffectCatalog.TrackEffects | RemovedEffects.RepeatEvents)),
      "Only Tiles removes the remaining effects without changing tracks or their repeat scheduling."
    );
    foreach (
      LevelEventType type in new[]
      {
        LevelEventType.ColorTrack,
        LevelEventType.RecolorTrack,
        LevelEventType.AnimateTrack,
        LevelEventType.MoveTrack,
        LevelEventType.PositionTrack,
        LevelEventType.ChangeTrack,
        LevelEventType.RepeatEvents,
      }
    )
      Check(!EffectLevelFilter.RemovesEvent(type, tiles), "Only Tiles must preserve " + type);
    foreach (
      LevelEventType type in new[]
      {
        LevelEventType.MoveCamera,
        LevelEventType.Flash,
        LevelEventType.SetFilter,
        LevelEventType.CustomBackground,
        LevelEventType.AddParticle,
        LevelEventType.SetPlanetRotation,
        LevelEventType.PlaySound,
      }
    )
      Check(EffectLevelFilter.RemovesEvent(type, tiles), "Only Tiles must remove non-track event " + type);
    Check((tiles & RemovedEffects.Decorations) != 0, "Only Tiles must remove non-gameplay decorations.");
  }

  private static void DefaultMigration()
  {
    string[] oldIds = { "focus", "calm", "minimal" };
    string[] newIds = { EffectSettings.RemoveAllId, EffectSettings.NoFiltersId, EffectSettings.OnlyTilesId };
    for (int i = 0; i < oldIds.Length; i++)
    {
      EffectSettings settings = ReadLegacy();
      Check(settings.DefaultsVersion == 0, "An old XML document without the version field must be recognized.");
      settings.ActivePresetId = oldIds[i];
      settings.Normalize();
      Check(
        settings.DefaultsVersion == EffectSettings.CurrentDefaultsVersion && settings.Enabled,
        "Migration must be versioned and preserve the master switch."
      );
      Check(
        settings.Presets.Count == 3 && settings.ActivePresetId == newIds[i],
        "An untouched active preset must map to its replacement."
      );
      string snapshot = Write(settings);
      settings.Normalize();
      Check(Write(settings) == snapshot, "Migration and normalization must be idempotent.");
      EffectSettings restored = Read(snapshot);
      restored.Normalize();
      Check(Write(restored) == snapshot, "A migrated XML roundtrip must not duplicate or remigrate defaults.");
    }

    EffectSettings customized = ReadLegacy();
    customized.Presets[0].Name = "My focus";
    customized.Presets[1].Effects |= RemovedEffects.TrackColors;
    customized.Presets.Add(
      new EffectPreset
      {
        Id = "my-copy",
        Name = "My minimal",
        Effects = customized.Presets[2].Effects,
      }
    );
    customized.ActivePresetId = "calm";
    RemovedEffects customMask = customized.Presets[1].Effects;
    customized.Normalize();
    Check(
      customized.Presets.Count == 4 && customized.Presets[0].Id == "focus" && customized.Presets[0].Name == "My focus",
      "Renamed defaults and unrelated custom presets must survive migration."
    );
    Check(
      customized.ActivePresetId == "calm" && customized.ActiveEffects == customMask,
      "Edited defaults must retain their identity, effects and active selection."
    );
    Check(
      customized.Presets[2].Id == EffectSettings.OnlyTilesId && customized.Presets[3].Id == "my-copy",
      "Only untouched original definitions may be replaced."
    );
    customized.Presets[0].Name = "Focus";
    customized.Normalize();
    Check(customized.Presets[0].Id == "focus", "Later edits must never trigger the one-time migration again.");

    EffectSettings collision = ReadLegacy();
    collision.Presets.Add(
      new EffectPreset
      {
        Id = EffectSettings.RemoveAllId,
        Name = "Remove All",
        Effects = RemovedEffects.None,
      }
    );
    collision.Normalize();
    Check(
      collision.Presets[3].Id == EffectSettings.RemoveAllId
        && collision.Presets[3].Name == "Remove All"
        && collision.Presets[3].Effects == RemovedEffects.None,
      "A custom preset owning the new name or ID must remain unchanged."
    );
    Check(
      collision.ActivePresetId == collision.Presets[0].Id
        && collision.Presets[0].Id != EffectSettings.RemoveAllId
        && collision.Presets[0].Name == "Remove All 2",
      "The migrated selection must follow its unique replacement, not a colliding custom preset."
    );

    EffectSettings deleted = ReadLegacy();
    deleted.Remove("focus");
    deleted.Normalize();
    Check(
      deleted.Presets.Count == 2 && !deleted.Presets.Exists(p => p.Id == EffectSettings.RemoveAllId),
      "A deleted default must not be reintroduced during migration."
    );
    EffectSettings empty = ReadLegacy();
    empty.Presets.Clear();
    empty.Normalize();
    Check(empty.Presets.Count == 0 && !empty.Enabled, "An intentionally empty old gallery must stay empty.");

    EffectSettings newer = ReadLegacy();
    newer.DefaultsVersion = EffectSettings.CurrentDefaultsVersion + 1;
    newer.Normalize();
    Check(
      newer.DefaultsVersion == EffectSettings.CurrentDefaultsVersion + 1 && newer.Presets[0].Id == "focus",
      "Settings from a newer defaults version must not be migrated backwards."
    );
  }

  private static EffectSettings ReadLegacy() =>
    Read(
      @"<EffectSettings>
    <Enabled>true</Enabled><ActivePresetId>focus</ActivePresetId><Presets>
      <EffectPreset><Id>focus</Id><Name>Focus</Name><Effects>CameraMotion ScreenFlash ScreenShake Filters AdvancedFilters Backgrounds Decorations Particles</Effects></EffectPreset>
      <EffectPreset><Id>calm</Id><Name>Calm</Name><Effects>ScreenFlash ScreenShake Filters AdvancedFilters</Effects></EffectPreset>
      <EffectPreset><Id>minimal</Id><Name>Minimal</Name><Effects>CameraMotion ScreenFlash ScreenShake Filters AdvancedFilters Backgrounds Decorations Particles TrackAnimations TrackMovement TrackColors TrackOpacity PlanetScale FrameRate</Effects></EffectPreset>
    </Presets></EffectSettings>"
    );

  private static EffectSettings Read(string xml)
  {
    using var input = new StringReader(xml);
    return (EffectSettings)new XmlSerializer(typeof(EffectSettings)).Deserialize(input);
  }

  private static string Write(EffectSettings settings)
  {
    using var output = new StringWriter();
    new XmlSerializer(typeof(EffectSettings)).Serialize(output, settings);
    return output.ToString();
  }
}
