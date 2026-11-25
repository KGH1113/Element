using System;
using System.Collections.Generic;
using Element.Configuration.Gameplay;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Input;
using ElementUI;
using UnityEngine;
using UnityEngine.EventSystems;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Chatter;

/// <summary>Tweaks → Chatter blocker: the switch, its threshold, keys it never blocks and what it has blocked.</summary>
internal sealed class ChatterViewModel : SettingsPageViewModel
{
  private long _shownBlocked = -1;

  /// <summary>KeyboardChatterBlocker is loaded too: two filters would drop presses twice.</summary>
  public readonly bool OtherBlockerRunning = IsAssemblyLoaded("KeyboardChatterBlocker");

  /// <summary>The presses blocked since the game started; the page reads it every frame while shown.</summary>
  public readonly State<int> Blocked = new(0);

  /// <summary>While true, each key pressed goes onto the ignored keys or comes off them.</summary>
  public bool Capturing;

  /// <summary>The ignored keys as words, such as "Space · X", or "None".</summary>
  public string IgnoredKeysLabel = "";

  public bool HasIgnoredKeys;

  public Computed<bool> CanChangeKeys { get; }
  public Computed<bool> CanClearKeys { get; }

  /// <summary>Starts listening for keys, or stops: Change keys, then Done.</summary>
  public Command ChangeKeys { get; }

  public Command ClearKeys { get; }

  public ChatterViewModel(SettingsWindowViewModel window, SettingsPageViewModel parent)
    : base(window, parent, L("채터 블로커", "Chatter blocker"))
  {
    CanChangeKeys = Computed(() => Settings.Gameplay.Chatter.Enabled);
    CanClearKeys = Computed(() => Settings.Gameplay.Chatter.Enabled && !Capturing && HasIgnoredKeys);
    ChangeKeys = Command(ToggleCapture, canExecute: CanChangeKeys);
    ClearKeys = Command(
      () =>
      {
        Settings.Gameplay.Chatter.IgnoredKeys.Clear();
        ShowIgnoredKeys();
      },
      canExecute: CanClearKeys
    );
    ShowIgnoredKeys();
    ReadBlocked();
  }

  /// <summary>The threshold in milliseconds, as the slider and the typed field edit it: whole, and within range.</summary>
  [ElementUI.Pure]
  public float Threshold
  {
    get => Settings.Gameplay.Chatter.WindowMs;
    set => Settings.Gameplay.Chatter.WindowMs = ChatterBlockerOptions.ClampWindow(Mathf.RoundToInt(value));
  }

  private void ToggleCapture()
  {
    if (Capturing)
    {
      Capturing = false;
      return;
    }
    Capturing = true;
    // Space and Enter would press the focused button instead of being added.
    EventSystem.current?.SetSelectedGameObject(null);
  }

  public override void CancelCapture()
  {
    if (!Capturing)
      return;
    Capturing = false;
    Runtime.Probe();
  }

  // Every frame while capturing; Escape ends it.
  public override bool TickCapture()
  {
    if (!Capturing)
      return false;
    if (!UnityEngine.Input.anyKeyDown)
      return true;
    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
    {
      CancelCapture();
      return true;
    }
    bool changed = false;
    int index = -1;
    while (KeyboardKeys.NextPressed(ref index, out KeyCode key))
      if (key != KeyCode.Escape && ChatterBlockerOptions.IsIgnorable(key))
      {
        Settings.Gameplay.Chatter.ToggleIgnored(key);
        changed = true;
      }
    if (!changed)
      return true;
    ShowIgnoredKeys();
    // A key press is neither a binding nor a command: apply, then show it.
    Window.Changed();
    Runtime.Probe();
    return true;
  }

  public override void Opened()
  {
    ShowIgnoredKeys();
    ReadBlocked();
  }

  public override void SettingsReplaced() => ShowIgnoredKeys();

  public override void Closed() => Capturing = false;

  // Every frame while the page shows; the text changes only when the count does.
  public override void Tick() => ReadBlocked();

  private void ReadBlocked()
  {
    long blocked = Window.Features.BlockedChatter?.Invoke() ?? 0;
    if (blocked == _shownBlocked)
      return;
    _shownBlocked = blocked;
    Blocked.Value = (int)Math.Min(blocked, int.MaxValue);
  }

  private void ShowIgnoredKeys()
  {
    List<KeyCode> keys = Settings.Gameplay.Chatter.IgnoredKeys;
    HasIgnoredKeys = keys != null && keys.Count > 0;
    if (!HasIgnoredKeys)
    {
      IgnoredKeysLabel = L("없음", "None");
      return;
    }
    var names = new string[keys.Count];
    for (int i = 0; i < names.Length; i++)
      names[i] = ShortcutKeys.KeyLabel(keys[i]);
    IgnoredKeysLabel = string.Join(" · ", names);
  }

  private static bool IsAssemblyLoaded(string name)
  {
    foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
      if (string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
  }
}
