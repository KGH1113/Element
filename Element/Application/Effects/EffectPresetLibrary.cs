using System;
using Element.Configuration.Effects;

namespace Element.Application.Effects;

/// <summary>
/// Adds, renames and removes the effect presets kept in <see cref="EffectSettings"/>.
/// A name is trimmed, at most <see cref="EffectSettings.MaximumNameLength"/>
/// characters, and no other preset has it in any case. No UI.
/// </summary>
internal sealed class EffectPresetLibrary
{
  private readonly EffectSettings _effects;

  public EffectPresetLibrary(EffectSettings effects) =>
    _effects = effects ?? throw new ArgumentNullException(nameof(effects));

  /// <param name="exceptId">The preset being renamed, which may keep its own name; null for a new preset.</param>
  public bool ValidateName(string name, string exceptId, out string normalized, out PresetNameProblem problem)
  {
    normalized = (name ?? "").Trim();
    problem =
      normalized.Length == 0 ? PresetNameProblem.Empty
      : normalized.Length > EffectSettings.MaximumNameLength ? PresetNameProblem.TooLong
      : NameTaken(normalized, exceptId) ? PresetNameProblem.Taken
      : PresetNameProblem.None;
    return problem == PresetNameProblem.None;
  }

  /// <summary>A new preset that removes nothing yet; the first one also becomes the selected preset.</summary>
  public bool TryAdd(string name, out EffectPreset preset, out PresetNameProblem problem)
  {
    preset = null;
    if (!ValidateName(name, null, out string normalized, out problem))
      return false;
    preset = new EffectPreset { Name = normalized };
    _effects.Presets.Add(preset);
    if (_effects.ActivePreset == null)
      _effects.ActivePresetId = preset.Id;
    return true;
  }

  public bool TryRename(string id, string name, out PresetNameProblem problem)
  {
    EffectPreset preset = _effects.Presets.Find(p => p.Id == id);
    if (!ValidateName(name, id, out string normalized, out problem) || preset == null)
      return false;
    preset.Name = normalized;
    return true;
  }

  /// <summary>
  /// Removes a preset. Removing the selected one selects its neighbor, and
  /// removing the last one turns effect removal off.
  /// </summary>
  public bool Remove(string id)
  {
    int index = _effects.Presets.FindIndex(p => p.Id == id);
    if (index < 0)
      return false;
    _effects.Presets.RemoveAt(index);
    if (_effects.ActivePresetId == id)
      _effects.ActivePresetId =
        _effects.Presets.Count > 0 ? _effects.Presets[Math.Min(index, _effects.Presets.Count - 1)].Id : "";
    if (_effects.Presets.Count == 0)
      _effects.Enabled = false;
    return true;
  }

  private bool NameTaken(string name, string exceptId) =>
    _effects.Presets.Exists(p => p.Id != exceptId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
