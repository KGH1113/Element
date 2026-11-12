using Element.Application.Updates;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Presentation.Notices;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>General: the profiles link, the settings shortcut, reduced motion, ElementNote and automatic updates.</summary>
internal sealed class GeneralModel : SettingsPageModel
{
  /// <summary>Automatic updates, at the end; null leaves the row out.</summary>
  public readonly IModUpdates Updates;
  public readonly bool HasUpdates;

  /// <summary>Profiles are linked at the top.</summary>
  public readonly bool HasProfiles;

  // Goes up each time General shows, so the update status is read again.
  public int UpdateChecks;

  /// <summary>Under the update switch: what happened to updates, and what to do about it.</summary>
  public Computed<string> UpdateStatus { get; }

  /// <summary>The key that opens and closes settings.</summary>
  public readonly ShortcutFieldModel Shortcut;

  public Command OpenProfiles { get; }

  public GeneralModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("일반", "General"))
  {
    Updates = window.Features.Updates;
    HasUpdates = Updates != null;
    HasProfiles = window.Features.Profiles != null;
    UpdateStatus = Computed(() => StatusOf(Updates, Updates.AutomaticUpdates, UpdateChecks));
    // Profiles is made after General; it is looked up when the link is pressed.
    OpenProfiles = Command(() => Window.Profiles?.Open());
    Shortcut = Own(
      new ShortcutFieldModel(
        () => Settings.SettingsShortcutKey,
        () => Settings.SettingsShortcutModifier,
        AssignShortcut,
        ResetShortcut,
        L("왼쪽 Ctrl + F9", "Left Ctrl + F9")
      )
    );
  }

  public override void Shown() => UpdateChecks++;

  public override bool TickCapture() => Shortcut.TickCapture();

  public override void CancelCapture() => Shortcut.Cancel();

  private string AssignShortcut(KeyCode key, KeyCode modifier)
  {
    if (Settings.ShortcutTaken(key, modifier, null, out EffectPreset owner))
      return ShortcutTakenText.For(owner);
    Settings.SettingsShortcutKey = key;
    Settings.SettingsShortcutModifier = modifier;
    // Recorded outside a control, so the window isn't told by one.
    Window.Changed();
    return null;
  }

  private void ResetShortcut()
  {
    Settings.SettingsShortcutKey = ElementSettings.DefaultSettingsShortcutKey;
    Settings.SettingsShortcutModifier = ElementSettings.DefaultSettingsShortcutModifier;
  }

  [ElementUI.Pure]
  private static string StatusOf(IModUpdates updates, bool automatic, int checks) => UpdateStatusText.For(updates);
}
