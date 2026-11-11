using System;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Notices;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Widgets;

namespace Element.Presentation.Screens;

/// <summary>
/// Switches the effect preset whose shortcut was pressed, saves, and says so
/// in a notice. Off during play, so keys pressed in a level never switch
/// presets.
/// </summary>
internal sealed class EffectPresetShortcuts
{
  private readonly ElementSettings _settings;
  private readonly Func<bool> _playing;
  private readonly Func<bool> _blocked;
  private readonly Action _apply;
  private readonly Action _presetsChanged;
  private readonly Func<bool> _save;
  private readonly UiToast _toast;

  /// <param name="playing">A level is being played.</param>
  /// <param name="blocked">Another surface owns the keyboard, such as an open window or a text field.</param>
  /// <param name="apply">Applies the settings to the game after a switch.</param>
  /// <param name="presetsChanged">The presets changed; the settings window shows them again.</param>
  /// <param name="save">Writes the settings; false when that failed.</param>
  public EffectPresetShortcuts(
    ElementSettings settings,
    Func<bool> playing,
    Func<bool> blocked,
    Action apply,
    Action presetsChanged,
    Func<bool> save,
    UiToast toast
  )
  {
    _settings = settings;
    _playing = playing;
    _blocked = blocked;
    _apply = apply;
    _presetsChanged = presetsChanged;
    _save = save;
    _toast = toast;
  }

  /// <summary>Runs every frame; frames without a new key press stop at the first check.</summary>
  public void Tick()
  {
    if (!UnityEngine.Input.anyKeyDown || _playing() || _blocked())
      return;
    EffectSettings settings = _settings.Effects;
    if (settings?.Presets == null)
      return;
    foreach (EffectPreset preset in settings.Presets)
    {
      if (preset == null || !preset.HasShortcut || !ShortcutKeys.Pressed(preset.ShortcutKey, preset.ShortcutModifier))
        continue;
      Switch(preset);
      return;
    }
  }

  private void Switch(EffectPreset preset)
  {
    EffectSettings effects = _settings.Effects;
    if (!effects.Toggle(preset.Id))
      return;
    _apply();
    _presetsChanged();
    bool saved = _save();
    (string title, string detail) = EffectPresetShortcutNotice.For(preset, effects.Enabled, saved);
    // Record mode hides mod overlays; only a failed save still needs to be seen.
    if (!_settings.RecordMode || !saved)
      _toast.Show(title, detail);
  }
}
