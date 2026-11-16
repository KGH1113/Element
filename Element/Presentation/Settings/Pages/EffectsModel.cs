using System;
using System.Collections.Generic;
using Element.Configuration.Effects;
using Element.Presentation.Settings.Window;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Pages;

/// <summary>
/// Effects: the effect removal switch and a card for each preset, which
/// applies it. A preset applied from outside the window, by its shortcut,
/// shows here as soon as the window probes.
/// </summary>
internal sealed class EffectsModel : SettingsPageModel
{
  /// <summary>A card per preset, in the presets' order. Replaced whenever the presets may have changed.</summary>
  public PresetCardModel[] Cards = Array.Empty<PresetCardModel>();

  public Computed<bool> HasPresets { get; }

  /// <summary>Under the switch: the preset in use, or what turning removal off does.</summary>
  public Computed<string> Status { get; }

  /// <summary>Opens the preset editor on a new preset.</summary>
  public Command NewPreset { get; }

  public EffectsModel(SettingsWindowViewModel window, SettingsPageModel parent)
    : base(window, parent, "Effects")
  {
    HasPresets = Computed(() => Cards.Length > 0);
    Status = Computed(() => StatusOf(Settings.Effects.Enabled, Settings.Effects.ActivePresetId, Cards));
    NewPreset = Command(() => Window.PresetEditor.Edit(null));
    Refresh();
  }

  public override void Opened() => Refresh();

  // Back from the preset editor, after a save or a delete.
  public override void Shown() => Refresh();

  public override void SettingsReplaced() => Refresh();

  /// <summary>Takes the presets as they are now. A preset keeps its card, so a card being clicked never goes away.</summary>
  public void Refresh()
  {
    List<EffectPreset> presets = Settings.Effects.Presets;
    PresetCardModel[] previous = Cards;
    var next = new PresetCardModel[presets.Count];
    for (int i = 0; i < next.Length; i++)
    {
      EffectPreset preset = presets[i];
      PresetCardModel card = Array.Find(previous, item => item != null && item.Id == preset.Id);
      if (card != null)
        card.Show(preset);
      else
        card = new PresetCardModel(Settings.Effects, preset, EditPreset);
      next[i] = card;
    }
    foreach (PresetCardModel card in previous)
      if (Array.IndexOf(next, card) < 0)
        card.Dispose();
    Cards = next;
  }

  private void EditPreset(string id)
  {
    EffectPreset preset = Settings.Effects.Presets.Find(item => item.Id == id);
    if (preset != null)
      Window.PresetEditor.Edit(preset);
  }

  protected override void OnDisposed()
  {
    foreach (PresetCardModel card in Cards)
      card.Dispose();
  }

  [ElementUI.Pure]
  private static string StatusOf(bool enabled, string activeId, PresetCardModel[] cards)
  {
    if (!enabled)
      return L("끄면 맵의 원래 이펙트가 표시됩니다.", "Off · original map effects are shown.");
    PresetCardModel active = Array.Find(cards, card => card.Id == activeId);
    return L("적용 프리셋: ", "Active preset: ") + active?.Name;
  }
}
