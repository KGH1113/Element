using Element.Configuration.Effects;
using Element.Infrastructure.Game.Effects;
using UnityEngine;
using static Element.Tests.Configuration.Effects.EffectPresetTests;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Configuration.Effects;

/// <summary>A preset's camera zoom and shortcut, each shortcut doing one thing.</summary>
internal static class EffectPresetShortcutTests
{
  internal static void Run()
  {
    CameraZoomFollowsThePreset();
    PresetShortcuts();
  }

  private static void CameraZoomFollowsThePreset()
  {
    Check(new EffectPreset().CameraZoom == 250, "A preset must start at Enhanced Effect Remover's 250% zoom.");
    var older = Read(
      @"<EffectSettings><Enabled>true</Enabled><ActivePresetId>old</ActivePresetId><Presets>
      <EffectPreset><Id>old</Id><Name>Old</Name><Effects>CameraMotion</Effects></EffectPreset>
    </Presets></EffectSettings>"
    );
    older.Normalize();
    Check(older.ActiveCameraZoom == 250, "Presets saved before the zoom existed must load at 250%.");

    var settings = new EffectSettings { Enabled = true };
    settings.ActivePreset.CameraZoom = 300;
    var restored = Read(Write(settings));
    restored.Normalize();
    Check(restored.ActiveCameraZoom == 300, "The chosen zoom must survive XML persistence.");
    Check(settings.ActivePreset.Copy().CameraZoom == 300, "Editing a copy of a preset must keep its zoom.");
    settings.ActivePreset.CameraZoom = 5000;
    settings.Normalize();
    Check(settings.ActiveCameraZoom == 1000, "A zoom above 1000% must load as 1000%.");
    settings.ActivePreset.CameraZoom = 0;
    settings.Normalize();
    Check(settings.ActiveCameraZoom == 100, "A zoom below 100% must load as 100%.");

    settings.ActivePreset.CameraZoom = 250;
    using var controller = new EffectsController(() => settings);
    Check(controller.CameraZoomForRendering == 250, "Playback must start with the preset's zoom.");
    settings.ActivePreset.CameraZoom = 400;
    controller.ApplySettings();
    Check(
      controller.RequiresRestart && controller.CameraZoomForRendering == 250,
      "A new zoom must wait for the next level start like any preset change."
    );
    Check(
      controller.CommitForPlayback() && controller.CameraZoomForRendering == 400,
      "A level start must rebuild with the new zoom, since the first tile holds the camera."
    );
    settings.Select(EffectSettings.NoFiltersId);
    controller.ApplySettings();
    controller.CommitForPlayback();
    settings.ActivePreset.CameraZoom = 700;
    controller.ApplySettings();
    Check(
      !controller.RequiresRestart,
      "A zoom change must not rebuild a level while the preset keeps the map's camera."
    );
  }

  private static void PresetShortcuts()
  {
    var settings = new EffectSettings();
    Check(settings.Presets.TrueForAll(p => !p.HasShortcut), "Default presets must start without a shortcut.");
    EffectPreset first = settings.Presets[0],
      second = settings.Presets[1];
    first.ShortcutKey = KeyCode.Alpha1;
    first.ShortcutModifier = KeyCode.LeftAlt;
    Check(
      settings.FindShortcut(KeyCode.Alpha1, KeyCode.LeftAlt) == first,
      "A shortcut must find the preset that uses it."
    );
    Check(
      settings.FindShortcut(KeyCode.Alpha1, KeyCode.None) == null
        && settings.FindShortcut(KeyCode.Alpha1, KeyCode.RightAlt) == null,
      "A shortcut must match its exact modifier, side included."
    );
    Check(
      settings.FindShortcut(KeyCode.Alpha1, KeyCode.LeftAlt, first.Id) == null,
      "The preset being edited must not conflict with its own shortcut."
    );
    Check(
      first.Copy().UsesShortcut(KeyCode.Alpha1, KeyCode.LeftAlt),
      "Editing a copy of a preset must keep its shortcut."
    );

    var restored = Read(Write(settings));
    restored.Normalize();
    Check(
      restored.Presets[0].UsesShortcut(KeyCode.Alpha1, KeyCode.LeftAlt) && !restored.Presets[1].HasShortcut,
      "Shortcuts must survive XML persistence."
    );
    var older = Read(
      @"<EffectSettings><ActivePresetId>old</ActivePresetId><Presets>
      <EffectPreset><Id>old</Id><Name>Old</Name><Effects>Filters</Effects></EffectPreset>
    </Presets></EffectSettings>"
    );
    older.Normalize();
    Check(!older.Presets[0].HasShortcut, "Presets saved before shortcuts existed must load without one.");

    second.ShortcutKey = KeyCode.Alpha1;
    second.ShortcutModifier = KeyCode.LeftAlt;
    settings.Normalize();
    Check(first.HasShortcut && !second.HasShortcut, "A shortcut saved on two presets must stay only on the first.");
    second.ShortcutKey = KeyCode.LeftShift;
    settings.Normalize();
    Check(!second.HasShortcut, "A modifier alone cannot be a shortcut.");
    second.ShortcutKey = KeyCode.Mouse0;
    settings.Normalize();
    Check(!second.HasShortcut, "A mouse button cannot be a shortcut.");
    second.ShortcutKey = KeyCode.Alpha2;
    second.ShortcutModifier = KeyCode.A;
    settings.Normalize();
    Check(
      !second.HasShortcut && second.ShortcutModifier == KeyCode.None,
      "Only a modifier key can be held with a shortcut."
    );
    second.ShortcutKey = KeyCode.F5;
    second.ShortcutModifier = KeyCode.None;
    settings.Normalize();
    Check(second.UsesShortcut(KeyCode.F5, KeyCode.None), "A single key without a modifier is a valid shortcut.");

    var element = new Element.Configuration.ElementSettings { Effects = settings };
    Check(
      element.ShortcutTaken(KeyCode.F5, KeyCode.None, null, out EffectPreset owner) && owner == second,
      "The settings shortcut can't take a preset's shortcut, and the preset is named."
    );
    Check(
      element.ShortcutTaken(element.SettingsShortcutKey, element.SettingsShortcutModifier, first.Id, out owner)
        && owner == null,
      "A preset can't take the settings shortcut."
    );
    Check(
      !element.ShortcutTaken(KeyCode.F5, KeyCode.None, second.Id, out _)
        && element.ShortcutTaken(KeyCode.F5, KeyCode.None, first.Id, out owner)
        && owner == second,
      "A preset keeps its own shortcut but can't take another preset's."
    );

    settings.Enabled = false;
    settings.ActivePresetId = first.Id;
    Check(
      settings.Activate(second.Id) && settings.Enabled && settings.ActivePresetId == second.Id,
      "Activating a preset must select it and turn effect removal on."
    );
    Check(
      !settings.Activate("missing") && settings.ActivePresetId == second.Id,
      "Activating a missing preset must leave the active one."
    );

    Check(
      settings.Toggle(second.Id) && !settings.Enabled && settings.ActivePresetId == second.Id,
      "Pressing the shortcut of the preset that is on must turn effect removal off and keep it selected."
    );
    Check(
      settings.Toggle(second.Id) && settings.Enabled && settings.ActivePresetId == second.Id,
      "Pressing it again must turn the same preset back on."
    );
    Check(
      settings.Toggle(first.Id) && settings.Enabled && settings.ActivePresetId == first.Id,
      "Another preset's shortcut must switch to that preset, not turn effect removal off."
    );
    Check(
      !settings.Toggle("missing") && settings.Enabled && settings.ActivePresetId == first.Id,
      "A missing preset's shortcut must change nothing."
    );
  }
}
