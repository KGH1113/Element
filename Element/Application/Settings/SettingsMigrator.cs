using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Gameplay;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Defaults;
using UnityEngine;

namespace Element.Application.Settings;

/// <summary>
/// Brings saved settings from any earlier version up to
/// <see cref="ElementSettings.CurrentSettingsVersion"/> and normalizes them.
/// Runs on the settings file once it is read and on a profile's values before
/// they are used. Each step runs once, decided by the version the values were
/// saved in.
/// </summary>
internal static class SettingsMigrator
{
  // The chatter window before version 8, measured from the key's release.
  private const int ReleaseChatterWindowMs = 20;

  public static void Migrate(ElementSettings settings)
  {
    int version = settings.SettingsVersion;
    // Only replace the previous default. After the first migration users can
    // deliberately choose that green again without a later load changing it.
    if (
      version < 1
      && string.Equals(settings.HighlightColor?.Trim().TrimStart('#'), "7CCE00", StringComparison.OrdinalIgnoreCase)
    )
      settings.HighlightColor = "#BA712B";
    settings.Gameplay ??= new();
    settings.Gameplay.Judgement ??= new();
    if (version < 2)
    {
      settings.Gameplay.Judgement.HidePerfect = settings.HidePerfectJudgmentText;
    }
    MigrateGameplay(settings.Gameplay);
    settings.Effects ??= new();
    settings.Effects.Normalize();
    settings.OverlayLayouts ??= new();
    settings.OverlayFont = OverlayFontKey.Normalize(settings.OverlayFont);
    MigrateLayouts(settings.OverlayLayouts, version);
    // Left Alt + Tab was the old default and also switches windows on Windows.
    // Move only that untouched default; a custom shortcut stays as chosen.
    if (
      version < 7
      && settings.SettingsShortcutKey == KeyCode.Tab
      && settings.SettingsShortcutModifier == KeyCode.LeftAlt
    )
    {
      settings.SettingsShortcutKey = ElementSettings.DefaultSettingsShortcutKey;
      settings.SettingsShortcutModifier = ElementSettings.DefaultSettingsShortcutModifier;
    }
    // The chatter window used to count from the key's release; it now counts
    // from the last press, as KeyboardChatterBlocker does. Only the untouched
    // old default moves to the new one.
    if (version < 8 && settings.Gameplay.Chatter.WindowMs == ReleaseChatterWindowMs)
      settings.Gameplay.Chatter.WindowMs = ChatterBlockerOptions.DefaultWindowMs;
    // Animation became a switch on each value; the combos that always grew keep growing.
    if (version < 9)
      OverlayElementMigration.AnimateCombos(settings.OverlayLayouts);
    settings.Profiles ??= new();
    settings.SettingsVersion = Math.Max(version, ElementSettings.CurrentSettingsVersion);
  }

  /// <summary>Normalizes gameplay tweaks and runs their own versioned steps.</summary>
  public static void MigrateGameplay(GameplayTweaksSettings gameplay)
  {
    gameplay.Normalize();
    MigrateJudgement(gameplay.Judgement);
  }

  /// <summary>
  /// Before version 1, Perfect −, X-Perfect and Perfect + shared HidePerfect.
  /// X-Perfect keeps that visibility on upgrade, then the two vary separately.
  /// </summary>
  public static void MigrateJudgement(JudgementVisualOptions judgement)
  {
    if (judgement.Version >= 1)
      return;
    judgement.HideXPerfect = judgement.HidePerfect;
    judgement.Version = 1;
  }

  private static void MigrateLayouts(List<OverlayLayoutSettings> layouts, int version)
  {
    // Presets from before 1.1 become texts already in the current form; texts
    // saved by 1.1.0 are rewritten for split values and units.
    if (version < 3)
      OverlayElementMigration.Migrate(layouts);
    else if (version < 4)
      OverlayElementMigration.MigrateFormulas(layouts);
    // Built-ins added by an update, such as the progress bar, arrive once;
    // deleting one afterwards keeps it gone.
    if (version < 6)
    {
      OverlayElementMigration.UpgradeProgressBar(layouts);
      OverlayElementDefaults.AddMissing(layouts);
    }
  }
}
