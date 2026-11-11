using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ADOFAI;
using Element.Configuration.Effects;
using Element.Infrastructure.Game.Effects;
using Element.Infrastructure.Game.Patches;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Infrastructure.Game;

/// <summary>
/// A preset filters only what the game plays: the editor gets its level back as it
/// was, decorations included.
/// </summary>
internal static class EffectPlaybackTests
{
  internal static void Run()
  {
    FilteringAndPlaybackBoundary();
    EditorReturnKeepsVisualPreset();
    EditorDecorationListPreservesSource();
    EditorReturnScopeLifetime();
  }

  private static void FilteringAndPlaybackBoundary()
  {
    Check(
      (GameHooks.Effects?.ForRendering ?? RemovedEffects.None) == RemovedEffects.None,
      "An inactive remover must leave editor visuals intact."
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
    RemovedEffects initial = controller.Applied;
    settings.Select(EffectSettings.NoFiltersId);
    controller.ApplySettings();
    Check(
      controller.RequiresRestart && controller.Applied == initial,
      "Changing presets must not rebuild a running level."
    );
    Check(
      controller.CommitForPlayback() && !controller.RequiresRestart,
      "A playback boundary must apply the pending preset."
    );
    Check(controller.Applied == settings.ActiveEffects, "The playback snapshot must match the selected preset.");
    settings.Enabled = false;
    controller.ApplySettings();
    Check(controller.RequiresRestart, "Disabling must schedule restoration at the next playback boundary.");
    controller.CommitForPlayback();
    Check(controller.Applied == RemovedEffects.None, "Disabling must restore vanilla event processing.");
    controller.Dispose();
    Check(controller.ForRendering == RemovedEffects.None, "Disposal must release the patches' visual preset.");
  }

  private static void EditorReturnKeepsVisualPreset()
  {
    var settings = new EffectSettings { Enabled = true };
    using var controller = new EffectsController(() => settings);
    RemovedEffects applied = controller.Applied;
    Check(controller.ForRendering == applied, "Opening the editor must use the applied visual preset.");
    for (int retry = 0; retry < 11; retry++)
    {
      controller.CommitForPlayback();
      Check(
        controller.ForRendering == applied,
        "Repeated failures must keep the same visual filter through asset reloads."
      );
    }

    settings.Enabled = false;
    controller.ApplySettings();
    using (controller.BeginEditorReturn())
    {
      Check(
        !controller.CommitForPlayback() && controller.Applied == applied && controller.RequiresRestart,
        "Editor return must defer pending settings instead of rebuilding the level with a different preset."
      );
      Check(
        controller.ForRendering == applied,
        "ResetScene, ReloadAssets and decoration setup on return must keep removed images filtered out."
      );
    }
    Check(
      !controller.ReturningToEditor && controller.ForRendering == applied,
      "The editor must retain the filtered preview and decoration list after returning from play."
    );
    Check(
      controller.CommitForPlayback() && controller.ForRendering == RemovedEffects.None,
      "The next level load or playback boundary must apply a requested disable."
    );
  }

  private static void EditorDecorationListPreservesSource()
  {
    LevelEvent required = EventFixture(LevelEventType.AddDecoration, ("tag", "required"));
    LevelEvent visual = EventFixture(LevelEventType.AddDecoration, ("tag", "visual"));
    LevelEvent text = EventFixture(LevelEventType.AddText);
    LevelEvent particle = EventFixture(LevelEventType.AddParticle);
    var source = new LevelData(false)
    {
      levelEvents = new EventsArray<LevelEvent>
      {
        EventFixture(LevelEventType.SetConditionalEvents, ("perfectTag", "on-hit")),
        EventFixture(LevelEventType.MoveDecorations, ("eventTag", "on-hit"), ("tag", "required")),
      },
      decorations = new DecorationsArray<LevelEvent> { required, visual, text, particle },
    };
    var display = new List<LevelEvent>(source.decorations);
    EffectLevelFilter.FilterEditorDecorations(display, source, EffectCatalog.All);
    Check(
      display.Count == 1 && ReferenceEquals(display[0], required),
      "The editor list must hide removed decorations while preserving gameplay-linked originals."
    );
    Check(
      source.decorations.Count == 4 && ReferenceEquals(source.decorations[1], visual) && source.levelEvents.Count == 2,
      "Filtering the decoration list must not delete source entries used by saving, editing or undo."
    );
    EffectLevelFilter.FilterEditorDecorations(display, source, EffectCatalog.All);
    Check(display.Count == 1, "Repeated editor list refreshes must not change the visible subset.");
    display = new List<LevelEvent>(source.decorations);
    EffectLevelFilter.FilterEditorDecorations(display, source, RemovedEffects.Filters);
    Check(display.Count == 4, "A filter-only preset must preserve every decoration in the editor list.");
    EffectLevelFilter.FilterEditorDecorations(display, source, RemovedEffects.Particles);
    Check(
      display.Count == 3 && !display.Contains(particle),
      "Particle removal must filter particle entries independently."
    );

    source.levelEvents.Clear();
    display = new List<LevelEvent>(source.decorations);
    EffectLevelFilter.FilterEditorDecorations(display, source, EffectCatalog.All);
    Check(display.Count == 0, "Remove All must keep a purely visual level's decoration tab empty after editor return.");
    Check(source.decorations.Count == 4, "Hiding all decoration entries must still preserve source data.");
  }

  private static void EditorReturnScopeLifetime()
  {
    var settings = new EffectSettings { Enabled = true };
    using var controller = new EffectsController(() => settings);
    RemovedEffects applied = controller.ForRendering;
    IDisposable outer = controller.BeginEditorReturn();
    using (controller.BeginEditorReturn())
      Check(controller.ForRendering == applied, "Nested editor return must not expose original effects.");
    Check(controller.ReturningToEditor, "Ending a nested return must keep the outer return boundary.");
    outer.Dispose();
    outer.Dispose();
    Check(
      !controller.ReturningToEditor && controller.ForRendering == applied,
      "Duplicate disposal must release the return boundary without disabling the visual preset."
    );

    bool failed = false;
    try
    {
      using (controller.BeginEditorReturn())
        throw new InvalidOperationException("transition failed");
    }
    catch (InvalidOperationException)
    {
      failed = true;
    }
    Check(
      failed && !controller.ReturningToEditor && controller.ForRendering == applied,
      "A failed transition must release its return boundary while retaining the applied visual preset."
    );
    controller.Dispose();
    Check(controller.ForRendering == RemovedEffects.None, "Mod disposal must release the visual preset.");
  }

  private static LevelEvent EventFixture(LevelEventType type, params (string Key, string Value)[] properties)
  {
    // LevelEvent's constructor needs the game's loaded settings asset. These
    // read-only filtering fixtures need only the event type and property data.
    var result = (LevelEvent)RuntimeHelpers.GetUninitializedObject(typeof(LevelEvent));
    result.eventType = type;
    var data = new Dictionary<string, object>();
    foreach (var (key, value) in properties)
      data.Add(key, value);
    typeof(LevelEvent)
      .GetField("data", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
      .SetValue(result, data);
    return result;
  }
}
