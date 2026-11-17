using System;
using System.Collections.Generic;
using Element.Application.Effects;
using Element.Configuration.Effects;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Effects → a preset: its name, the effects it removes in tabs by group,
/// the camera zoom and the shortcut that applies it. Edits wait in a draft
/// until Save; Back leaves the preset as it was.
/// </summary>
internal sealed class PresetEditorModel : SettingsPageModel
{
  /// <summary>The Shortcut tab, after the five effect groups.</summary>
  public const int ShortcutTab = 5;

  private readonly EffectPresetLibrary _presets;

  // The preset being edited; null while the draft is a new preset.
  private EffectPreset _editing;

  /// <summary>The draft is a new preset: there is nothing to delete yet.</summary>
  public bool IsNew = true;

  /// <summary>The preset being edited, or null for a new one; names it may keep.</summary>
  public string EditingId;

  public string Name = "";

  /// <summary>Zoom percent of the camera that follows the planets once camera movement is removed.</summary>
  public float Zoom = EffectPreset.DefaultCameraZoom;

  public KeyCode ShortcutKey = KeyCode.None;
  public KeyCode ShortcutModifier = KeyCode.None;

  /// <summary>The tab shown: an <see cref="EffectGroup"/>, or <see cref="ShortcutTab"/>.</summary>
  public int Tab;

  /// <summary>Goes up each time a new preset opens, so its name field takes the keyboard.</summary>
  public int FocusName;

  public readonly string[] TabNames;

  /// <summary>Every effect, in the catalog's order.</summary>
  public readonly EffectChoiceModel[] Choices;

  /// <summary>Camera movement: it leads the Screen tab, with the zoom under it.</summary>
  public readonly EffectChoiceModel Camera;

  /// <summary>The effects of each group (by <see cref="EffectGroup"/>), camera movement left out of Screen.</summary>
  public readonly EffectChoiceModel[][] Groups;

  /// <summary>Why the typed name can't be used, or empty.</summary>
  public Computed<string> NameProblem { get; }
  public Computed<bool> CanSave { get; }

  public Command Save { get; }
  public Command Delete { get; }

  /// <summary>The draft's shortcut.</summary>
  public readonly ShortcutFieldModel Shortcut;

  public PresetEditorModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, L("새 프리셋", "New preset"))
  {
    _presets = new EffectPresetLibrary(Settings.Effects);
    var groups = (EffectGroup[])Enum.GetValues(typeof(EffectGroup));
    TabNames = new string[groups.Length + 1];
    for (int i = 0; i < groups.Length; i++)
      TabNames[i] = EffectCatalog.GroupName(groups[i], Korean);
    TabNames[ShortcutTab] = L("단축키", "Shortcut");

    Choices = new EffectChoiceModel[EffectCatalog.Entries.Length];
    var byGroup = new List<EffectChoiceModel>[groups.Length];
    for (int i = 0; i < byGroup.Length; i++)
      byGroup[i] = new List<EffectChoiceModel>();
    for (int i = 0; i < Choices.Length; i++)
    {
      EffectChoiceModel choice = Own(new EffectChoiceModel(EffectCatalog.Entries[i]));
      Choices[i] = choice;
      if (choice.Definition.Flag == RemovedEffects.CameraMotion)
        Camera = choice;
      else
        byGroup[(int)choice.Definition.Group].Add(choice);
    }
    Groups = new EffectChoiceModel[groups.Length][];
    for (int i = 0; i < groups.Length; i++)
      Groups[i] = byGroup[i].ToArray();

    NameProblem = Computed(() => ProblemOf(_presets, Name, EditingId));
    CanSave = Computed(() => Valid(_presets, Name, EditingId));
    Save = Command(SavePreset, CanSave);
    Delete = Command(DeletePreset);
    Shortcut = Own(
      new ShortcutFieldModel(
        () => ShortcutKey,
        () => ShortcutModifier,
        AssignShortcut,
        () =>
        {
          ShortcutKey = KeyCode.None;
          ShortcutModifier = KeyCode.None;
        },
        L("왼쪽 Alt + 1", "Left Alt + 1"),
        L("눌러서 단축키 지정", "Click to set a shortcut"),
        actNeedsShortcut: true
      )
    );
  }

  /// <summary>The effects the draft removes.</summary>
  public RemovedEffects DraftEffects
  {
    get
    {
      RemovedEffects effects = RemovedEffects.None;
      foreach (EffectChoiceModel choice in Choices)
        if (choice.Removed)
          effects |= choice.Definition.Flag;
      return effects;
    }
  }

  /// <summary>Opens a draft of <paramref name="preset"/>, or of a new preset when null, on its first tab.</summary>
  public void Edit(EffectPreset preset)
  {
    _editing = preset;
    IsNew = preset == null;
    EditingId = preset?.Id;
    Title = IsNew ? L("새 프리셋", "New preset") : L("프리셋 편집", "Edit preset");
    Name = preset?.Name ?? "";
    Zoom = preset?.CameraZoom ?? EffectPreset.DefaultCameraZoom;
    ShortcutKey = preset?.ShortcutKey ?? KeyCode.None;
    ShortcutModifier = preset?.ShortcutModifier ?? KeyCode.None;
    foreach (EffectChoiceModel choice in Choices)
      choice.Removed = preset != null && (preset.Effects & choice.Definition.Flag) != 0;
    Tab = (int)EffectGroup.Screen;
    Shortcut.Cancel();
    if (IsNew)
      FocusName++;
    Window.Show(this);
  }

  /// <summary>Every tab press ends shortcut capture, including pressing the selected tab.</summary>
  public void SelectTab(int tab)
  {
    Tab = tab;
    Shortcut.Cancel();
  }

  public override bool TickCapture() => Shortcut.TickCapture();

  public override void CancelCapture() => Shortcut.Cancel();

  private string AssignShortcut(KeyCode key, KeyCode modifier)
  {
    // A new preset has no id yet; "" is none, so every preset's shortcut counts.
    if (Settings.ShortcutTaken(key, modifier, EditingId ?? "", out EffectPreset owner))
      return ShortcutTakenText.For(owner);
    ShortcutKey = key;
    ShortcutModifier = modifier;
    return null;
  }

  private void SavePreset()
  {
    if (!_presets.ValidateName(Name, EditingId, out string name, out _))
      return;
    if (_editing == null)
    {
      if (!_presets.TryAdd(name, out _editing, out _))
        return;
    }
    else
      _presets.TryRename(_editing.Id, name, out _);
    _editing.Effects = DraftEffects;
    _editing.CameraZoom = EffectPreset.ClampCameraZoom(Mathf.RoundToInt(Zoom));
    _editing.ShortcutKey = ShortcutKey;
    _editing.ShortcutModifier = ShortcutModifier;
    Leave();
  }

  private void DeletePreset()
  {
    if (_editing == null)
      return;
    _presets.Remove(_editing.Id);
    Leave();
  }

  private void Leave()
  {
    Shortcut.Cancel();
    Window.Show(Parent);
  }

  // As the name is typed: an empty field says nothing yet.
  [ElementUI.Pure]
  private static string ProblemOf(EffectPresetLibrary presets, string name, string exceptId)
  {
    if (string.IsNullOrEmpty(name) || presets.ValidateName(name, exceptId, out _, out PresetNameProblem problem))
      return "";
    return problem switch
    {
      PresetNameProblem.Empty => L("프리셋 이름을 입력하세요.", "Give this preset a name."),
      PresetNameProblem.TooLong => L(
        "이름은 " + EffectSettings.MaximumNameLength + "자 이내로 입력하세요.",
        "Use " + EffectSettings.MaximumNameLength + " characters or fewer."
      ),
      _ => L(
        "같은 이름의 프리셋이 있어요. 다른 이름을 입력하세요.",
        "A preset already uses this name. Choose another."
      ),
    };
  }

  // The preset list changes only through this page, which is left when it does.
  [ElementUI.Pure]
  private static bool Valid(EffectPresetLibrary presets, string name, string exceptId) =>
    presets.ValidateName(name, exceptId, out _, out _);
}
