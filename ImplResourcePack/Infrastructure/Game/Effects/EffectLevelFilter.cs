using System;
using System.Collections.Generic;
using System.Reflection;
using ADOFAI;
using ImplResourcePack.Configuration.Effects;

namespace ImplResourcePack.Infrastructure.Game.Effects;

/// <summary>Produces render input without changing the editor's serializable level data.</summary>
internal static class EffectLevelFilter
{
  private static readonly MethodInfo MemberwiseCopy = typeof(object).GetMethod(
    "MemberwiseClone",
    BindingFlags.Instance | BindingFlags.NonPublic
  );
  private static readonly string[] ColorProperties =
  {
    "trackColor",
    "secondaryTrackColor",
    "trackColorType",
    "trackColorAnimDuration",
    "trackColorPulse",
    "trackPulseLength",
    "trackStyle",
    "trackTexture",
    "trackTextureScale",
    "trackGlowIntensity",
    "trackShadowColor",
  };

  public static LevelData Create(LevelData source, RemovedEffects effects, List<LevelEvent> events = null)
  {
    if (source == null || effects == RemovedEffects.None)
      return source;

    // A shallow copy preserves all game-version-specific metadata. Only collections
    // and settings that this filter writes are copied below; event originals stay intact.
    var result = (LevelData)MemberwiseCopy.Invoke(source, null);
    result.trackSettings = source.trackSettings.Copy();
    if (Has(effects, RemovedEffects.TrackColors))
      CopyProperties(
        new LevelEvent(0, LevelEventType.TrackSettings, GCS.settingsInfo["TrackSettings"]),
        result.trackSettings,
        ColorProperties
      );
    if (Has(effects, RemovedEffects.TrackAnimations))
      ResetAnimations(result.trackSettings);

    if (Has(effects, RemovedEffects.Backgrounds))
    {
      result.backgroundSettings = source.backgroundSettings.Copy();
      result.backgroundSettings["bgImage"] = "";
      result.backgroundSettings["backgroundColor"] = "101010ff";
      result.backgroundSettings["showDefaultBGIfNoImage"] = false;
      result.backgroundSettings["showDefaultBGTile"] = false;
      result.backgroundSettings["defaultBGShapeType"] = BGShapeType.Disabled;
      if (result.backgroundSettings.ContainsKey("customBGPrefab"))
        result.backgroundSettings["customBGPrefab"] = "";
      result.miscSettings = source.miscSettings.Copy();
      result.miscSettings["bgVideo"] = "";
    }

    List<LevelEvent> sourceEvents = events ?? source.levelEvents;
    var preservedTags = Has(effects, RemovedEffects.Decorations) ? GameplayDecorationTags(sourceEvents) : null;
    result.levelEvents = new EventsArray<LevelEvent>();
    foreach (LevelEvent item in sourceEvents)
    {
      if (ShouldRemove(item, effects, preservedTags))
        continue;
      result.levelEvents.Add(Transform(item, result.trackSettings, effects));
    }
    result.decorations = new DecorationsArray<LevelEvent>();
    foreach (LevelEvent item in source.decorations)
      if (!ShouldRemove(item, effects, preservedTags))
        result.decorations.Add(item);
    return result;
  }

  public static bool RemovesEvent(LevelEventType type, RemovedEffects effects)
  {
    RemovedEffects required = type switch
    {
      LevelEventType.MoveCamera => RemovedEffects.CameraMotion,
      LevelEventType.Flash => RemovedEffects.ScreenFlash,
      LevelEventType.ShakeScreen => RemovedEffects.ScreenShake,
      LevelEventType.SetFilter
      or LevelEventType.HallOfMirrors
      or LevelEventType.Bloom
      or LevelEventType.ScreenTile
      or LevelEventType.ScreenScroll => RemovedEffects.Filters,
      LevelEventType.SetFilterAdvanced => RemovedEffects.AdvancedFilters,
      LevelEventType.CustomBackground => RemovedEffects.Backgrounds,
      LevelEventType.AddParticle or LevelEventType.SetParticle or LevelEventType.EmitParticle =>
        RemovedEffects.Particles,
      LevelEventType.AnimateTrack => RemovedEffects.TrackAnimations,
      LevelEventType.MoveTrack => RemovedEffects.TrackMovement,
      LevelEventType.PositionTrack => RemovedEffects.TrackPosition,
      LevelEventType.ColorTrack or LevelEventType.RecolorTrack => RemovedEffects.TrackColors,
      LevelEventType.SetPlanetRotation => RemovedEffects.PlanetOrbit,
      LevelEventType.ScalePlanets => RemovedEffects.PlanetScale,
      LevelEventType.ScaleRadius => RemovedEffects.PlanetRadius,
      LevelEventType.SetHitsound or LevelEventType.PlaySound => RemovedEffects.HitSounds,
      LevelEventType.SetHoldSound => RemovedEffects.HoldSounds,
      LevelEventType.Hide => RemovedEffects.HideIcons,
      LevelEventType.RepeatEvents => RemovedEffects.RepeatEvents,
      LevelEventType.SetFrameRate => RemovedEffects.FrameRate,
      LevelEventType.Checkpoint => RemovedEffects.Checkpoints,
      _ => RemovedEffects.None,
    };
    return required != RemovedEffects.None && Has(effects, required);
  }

  private static bool ShouldRemove(LevelEvent item, RemovedEffects effects, HashSet<string> preservedTags)
  {
    if (RemovesEvent(item.eventType, effects))
      return true;
    return preservedTags != null
      && IsDecorationEvent(item.eventType)
      && !MatchesTags(item, "tag", preservedTags)
      && !MatchesTags(item, "eventTag", preservedTags);
  }

  private static LevelEvent Transform(LevelEvent item, LevelEvent trackSettings, RemovedEffects effects)
  {
    bool opacity =
      Has(effects, RemovedEffects.TrackOpacity)
      && (item.eventType == LevelEventType.MoveTrack || item.eventType == LevelEventType.PositionTrack)
      && item.ContainsKey("opacity");
    bool legacy =
      item.eventType == LevelEventType.ChangeTrack
      && Has(effects, RemovedEffects.TrackAnimations | RemovedEffects.TrackColors);
    if (!opacity && !legacy)
      return item;
    LevelEvent result = item.Copy();
    if (opacity)
    {
      result["opacity"] = 100f;
      result.disabled["opacity"] = false;
    }
    if (legacy)
    {
      if (Has(effects, RemovedEffects.TrackColors))
        CopyProperties(trackSettings, result, ColorProperties);
      if (Has(effects, RemovedEffects.TrackAnimations))
        ResetAnimations(result);
    }
    return result;
  }

  private static void ResetAnimations(LevelEvent settings)
  {
    settings["trackAnimation"] = TrackAnimationType.None;
    settings["trackDisappearAnimation"] = TrackAnimationType2.None;
    settings["beatsAhead"] = 8f;
    settings["beatsBehind"] = 0f;
  }

  private static void CopyProperties(LevelEvent source, LevelEvent destination, string[] names)
  {
    foreach (string name in names)
      if (source.ContainsKey(name))
        destination[name] = source[name];
  }

  private static HashSet<string> GameplayDecorationTags(IEnumerable<LevelEvent> events)
  {
    var tags = new HashSet<string>(StringComparer.Ordinal);
    foreach (LevelEvent item in events)
    {
      if (item.eventType != LevelEventType.SetConditionalEvents && item.eventType != LevelEventType.SetInputEvent)
        continue;
      foreach (KeyValuePair<string, object> property in item.GetData())
        if (property.Key.EndsWith("Tag", StringComparison.OrdinalIgnoreCase) && property.Value is string value)
          AddTags(value, tags);
    }
    // Follow references in linear time; a long chain must not repeatedly rescan
    // a large map. The same tag can reference several decoration events.
    var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    foreach (LevelEvent item in events)
    {
      if (!IsDecorationEvent(item.eventType) || !item.ContainsKey("eventTag") || !item.ContainsKey("tag"))
        continue;
      string eventTags = item.GetString("eventTag");
      string decorationTags = item.GetString("tag");
      if (string.IsNullOrWhiteSpace(eventTags) || string.IsNullOrWhiteSpace(decorationTags))
        continue;
      string[] targets = decorationTags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (string trigger in eventTags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
      {
        if (!references.TryGetValue(trigger, out List<string> list))
          references.Add(trigger, list = new List<string>());
        list.AddRange(targets);
      }
    }
    var pending = new Queue<string>(tags);
    while (pending.Count > 0)
      if (references.TryGetValue(pending.Dequeue(), out List<string> targets))
        foreach (string target in targets)
          if (tags.Add(target))
            pending.Enqueue(target);
    return tags;
  }

  private static bool MatchesTags(LevelEvent item, string property, HashSet<string> tags)
  {
    if (!item.ContainsKey(property) || tags.Count == 0)
      return false;
    string value = item.GetString(property);
    if (string.IsNullOrEmpty(value))
      return false;
    foreach (string part in value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
      if (tags.Contains(part))
        return true;
    return false;
  }

  private static void AddTags(string value, HashSet<string> tags)
  {
    if (string.IsNullOrWhiteSpace(value))
      return;
    foreach (string part in value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
      if (part != "None" && part != "없음")
        tags.Add(part);
  }

  private static bool IsDecorationEvent(LevelEventType type) =>
    type
      is LevelEventType.DecorationSettings
        or LevelEventType.AddDecoration
        or LevelEventType.AddText
        or LevelEventType.SetText
        or LevelEventType.MoveDecorations
        or LevelEventType.AddObject
        or LevelEventType.SetObject
        or LevelEventType.SetDefaultText;

  private static bool Has(RemovedEffects value, RemovedEffects flag) => (value & flag) != 0;
}
