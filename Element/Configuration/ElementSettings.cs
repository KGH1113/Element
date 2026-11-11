using System.Collections.Generic;
using Element.Configuration.Gameplay;
using Element.Configuration.Overlay;
using UnityEngine;
using UnityModManagerNet;

namespace Element.Configuration;

[CopiedInPlace]
public sealed class ElementSettings : UnityModManager.ModSettings
{
  public bool HidePerfectJudgmentText = true;

  public bool RecordMode;

  public bool NativeEditorTrackpad = true;

  public bool ShowDmNoteInGame = true;

  /// <summary>Open the bundled ElementNote with the game; it closes when the game quits.</summary>
  public bool AutoOpenCloseDmNote = true;

  /// <summary>The version SettingsMigrator brings settings to. Raise it with each new migration step.</summary>
  public const int CurrentSettingsVersion = 9;

  public const string DefaultHighlightColor = "#BA712B";

  public int SettingsVersion;
  public string HighlightColor = DefaultHighlightColor;
  public List<string> RecentHighlightColors = new();
  public bool ReduceUiMotion;
  public bool LayoutSnapping = true;

  /// <summary>The overlay texts' default font as an OverlayFontKey; empty uses the bundled font.</summary>
  public string OverlayFont = string.Empty;

  public GameplayTweaksSettings Gameplay = new();
  public Effects.EffectSettings Effects = new();

  public const KeyCode DefaultSettingsShortcutKey = KeyCode.F9;
  public const KeyCode DefaultSettingsShortcutModifier = KeyCode.LeftControl;

  public KeyCode SettingsShortcutKey = DefaultSettingsShortcutKey;
  public KeyCode SettingsShortcutModifier = DefaultSettingsShortcutModifier;

  /// <summary>
  /// Whether the shortcut already opens settings or applies a preset, since one
  /// shortcut does one thing. <paramref name="presetId"/> is the preset being
  /// given the shortcut, or null for the settings shortcut. <paramref name="owner"/>
  /// is the preset that has it, or null when it opens settings.
  /// </summary>
  public bool ShortcutTaken(KeyCode key, KeyCode modifier, string presetId, out Effects.EffectPreset owner)
  {
    owner = null;
    if (presetId != null && key == SettingsShortcutKey && modifier == SettingsShortcutModifier)
      return true;
    owner = Effects.FindShortcut(key, modifier, presetId);
    return owner != null;
  }

  public List<OverlayLayoutSettings> OverlayLayouts = new();

  /// <summary>
  /// Every profile, the one in use included. The values above belong to the
  /// profile in use; the others keep theirs in <see cref="Profiles.ElementProfile.Content"/>.
  /// </summary>
  public List<Profiles.ElementProfile> Profiles = new();

  public string ActiveProfileId = "";

  public override void Save(UnityModManager.ModEntry modEntry)
  {
    Save(this, modEntry);
  }
}
