using System;
using Element.Configuration.Effects;
using Element.Presentation.Ui.Input;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>One preset's card in Effects: its name, how much it removes, its shortcut, whether it is on, and what clicking does.</summary>
internal sealed class PresetCardModel : ViewModel
{
  public readonly EffectSettings Effects;

  public readonly string Id;
  public string Name = "";

  /// <summary>"22 effects removed".</summary>
  public string Removes = "";

  /// <summary>The shortcut that applies it, spelled out, or empty.</summary>
  public string Shortcut = "";

  public Computed<bool> Selected { get; }

  /// <summary>Active, Selected · Off, or Click to apply.</summary>
  public Computed<string> State { get; }

  /// <summary>Selects the preset and turns effect removal on.</summary>
  public Command Apply { get; }

  /// <summary>Opens the preset editor on it.</summary>
  public Command Edit { get; }

  public PresetCardModel(EffectSettings effects, EffectPreset preset, Action<string> edit)
  {
    Effects = effects;
    Id = preset.Id;
    Show(preset);
    Selected = Computed(() => Effects.ActivePresetId == Id);
    State = Computed(() => StateOf(Selected.Value, Effects.Enabled));
    Apply = Command(() => Effects.Activate(Id));
    Edit = Command(() => edit(Id));
  }

  /// <summary>Takes the preset's name, effects and shortcut as they are now.</summary>
  public void Show(EffectPreset preset)
  {
    Name = preset.Name;
    Removes = EffectCatalog.Count(preset.Effects) + L("개 효과 제거", " effects removed");
    Shortcut = preset.HasShortcut ? ShortcutKeys.TextLabel(preset.ShortcutKey, preset.ShortcutModifier) : "";
  }

  [ElementUI.Pure]
  private static string StateOf(bool selected, bool enabled) =>
    !selected ? L("선택하여 적용", "Click to apply")
    : enabled ? L("적용 중", "Active")
    : L("선택됨 · 꺼짐", "Selected · Off");
}
