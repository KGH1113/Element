using System;
using Element.Configuration.Effects;
using Element.Infrastructure.Game.Lifecycle;
using Element.Presentation.Settings;

namespace Element.Infrastructure.Game.Effects;

/// <summary>
/// Applies the effect preset whose shortcut was pressed. Off during play, so
/// keys pressed in a level never switch presets.
/// </summary>
internal sealed class EffectPresetShortcuts
{
  private readonly Func<EffectSettings> _settings;
  private readonly Func<bool> _blocked;
  private readonly Action<EffectPreset> _pressed;

  /// <param name="blocked">Another surface owns the keyboard, such as an open window or a text field.</param>
  /// <param name="pressed">A preset's shortcut was pressed; applies it.</param>
  public EffectPresetShortcuts(Func<EffectSettings> settings, Func<bool> blocked, Action<EffectPreset> pressed)
  {
    _settings = settings;
    _blocked = blocked;
    _pressed = pressed;
  }

  /// <summary>Runs every frame; frames without a new key press stop at the first check.</summary>
  public void Tick()
  {
    if (!UnityEngine.Input.anyKeyDown || GameplayState.IsPlaying || _blocked())
      return;
    EffectSettings settings = _settings();
    if (settings?.Presets == null)
      return;
    foreach (EffectPreset preset in settings.Presets)
    {
      if (preset == null || !preset.HasShortcut || !ShortcutKeys.Pressed(preset.ShortcutKey, preset.ShortcutModifier))
        continue;
      _pressed(preset);
      return;
    }
  }
}
