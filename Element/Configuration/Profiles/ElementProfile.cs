using System;
using System.Collections.Generic;
using Element.Configuration.Effects;

namespace Element.Configuration.Profiles;

/// <summary>A named set of settings the player switches between and shares as a file.</summary>
public sealed class ElementProfile
{
  public const int MaximumNameLength = 40;

  public string Id = Guid.NewGuid().ToString("N");
  public string Name = "";

  /// <summary>
  /// The saved values of a profile that is not in use. Null for the profile in
  /// use, whose values are the live settings.
  /// </summary>
  public ProfileContent Content;
}

/// <summary>
/// What a profile carries: the overlay, effect presets, gameplay tweaks, the
/// highlight color and the overlay's default font. The chatter filter suits
/// one keyboard, and general preferences such as shortcuts suit one PC, so
/// every profile shares those.
/// </summary>
public sealed class ProfileContent
{
  /// <summary>The settings version the values were saved in; applying them migrates from it.</summary>
  public int SettingsVersion;
  public string HighlightColor = ElementSettings.DefaultHighlightColor;

  // An OverlayFontKey; the font itself stays on each PC, found by its name.
  public string OverlayFont = "";
  public GameplayTweaksSettings Gameplay = new();
  public EffectSettings Effects = new();
  public List<OverlayLayoutSettings> OverlayLayouts = new();

  /// <summary>A copy of the profile values in <paramref name="settings"/>.</summary>
  public static ProfileContent Capture(ElementSettings settings)
  {
    ProfileContent copy = SettingsXml.Clone(
      new ProfileContent
      {
        SettingsVersion = settings.SettingsVersion,
        HighlightColor = settings.HighlightColor,
        OverlayFont = settings.OverlayFont ?? "",
        Gameplay = settings.Gameplay,
        Effects = settings.Effects,
        OverlayLayouts = settings.OverlayLayouts,
      }
    );
    copy.Gameplay.Chatter = null;
    return copy;
  }

  /// <summary>
  /// A migrated, normalized copy of these values, ready for <see cref="Apply"/>.
  /// Throws when the values cannot be used, before anything live changes.
  /// </summary>
  public ElementSettings Prepare()
  {
    ProfileContent copy = SettingsXml.Clone(this);
    ElementSettings settings = new()
    {
      SettingsVersion = copy.SettingsVersion,
      HighlightColor = string.IsNullOrWhiteSpace(copy.HighlightColor)
        ? ElementSettings.DefaultHighlightColor
        : copy.HighlightColor,
      OverlayFont = copy.OverlayFont ?? "",
      Gameplay = copy.Gameplay ?? new(),
      Effects = copy.Effects ?? new(),
      OverlayLayouts = copy.OverlayLayouts ?? new(),
    };
    settings.OverlayLayouts.RemoveAll(layout => layout == null);
    settings.Migrate();
    return settings;
  }

  /// <summary>
  /// Puts prepared values into <paramref name="live"/> in place: the settings
  /// window, the canvas and the tweaks keep the objects they already hold.
  /// </summary>
  public static void Apply(ElementSettings prepared, ElementSettings live)
  {
    live.HighlightColor = prepared.HighlightColor;
    live.OverlayFont = prepared.OverlayFont;
    // The incoming values take this PC's chatter filter.
    SettingsCopy.Into(prepared.Gameplay.Chatter, live.Gameplay.Chatter);
    SettingsCopy.Into(live.Gameplay, prepared.Gameplay);
    SettingsCopy.Into(live.Effects, prepared.Effects);
    // The settings shortcut is this PC's; a preset from another PC may use it.
    foreach (EffectPreset preset in live.Effects.Presets)
      if (preset.UsesShortcut(live.SettingsShortcutKey, live.SettingsShortcutModifier))
        preset.ClearShortcut();
    live.OverlayLayouts.Clear();
    live.OverlayLayouts.AddRange(prepared.OverlayLayouts);
  }
}
