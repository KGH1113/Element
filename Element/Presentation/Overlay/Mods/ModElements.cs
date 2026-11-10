using System;
using System.Collections.Generic;
using Element.Configuration;
using ElementKit.Elements;
using ElementKit.Hosting;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// The element types other mods add through ElementKit, found by the key
/// layouts save (<c>ModId/type</c>). Rebuilt when a mod attaches, detaches,
/// or is turned on or off; a frame with nothing new reads one flag per mod.
/// </summary>
internal sealed class ModElements
{
  private readonly Dictionary<string, ElementType> _types = new(StringComparer.Ordinal);
  private readonly List<ElementType> _offered = new();
  private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
  private readonly Action<string> _warn;
  private AttachedMod[] _mods = Array.Empty<AttachedMod>();
  private bool[] _active = Array.Empty<bool>();
  private int _registry = -1;

  public ModElements(Action<string> warn) => _warn = warn ?? (_ => { });

  /// <summary>Advances whenever a type comes or goes, so elements look theirs up again.</summary>
  public int Version { get; private set; }

  /// <summary>The types players can add now, in the order their mods attached.</summary>
  public IReadOnlyList<ElementType> Offered => _offered;

  /// <summary>Once a frame, before elements refresh.</summary>
  public void Refresh()
  {
    int registry = KitRegistry.Version;
    bool changed = false;
    if (registry != _registry)
    {
      _registry = registry;
      _mods = KitRegistry.Snapshot;
      _active = new bool[_mods.Length];
      for (int i = 0; i < _mods.Length; i++)
        _active[i] = IsActive(_mods[i]);
      changed = true;
    }
    else
      for (int i = 0; i < _mods.Length; i++)
      {
        bool active = IsActive(_mods[i]);
        if (active == _active[i])
          continue;
        _active[i] = active;
        changed = true;
      }
    if (!changed)
      return;
    Rebuild();
    Version++;
  }

  /// <summary>The type that draws <paramref name="templateId"/>; null while its mod is missing or off.</summary>
  public ElementType Find(string templateId) =>
    templateId != null && _types.TryGetValue(templateId, out ElementType type) ? type : null;

  /// <summary>Whether the mod that draws <paramref name="templateId"/> is installed but turned off.</summary>
  public bool IsTurnedOff(string templateId)
  {
    string mod = OverlayElementKinds.ModOf(templateId);
    for (int i = 0; i < _mods.Length; i++)
      if (_mods[i].Id == mod)
        return !_active[i];
    return false;
  }

  /// <summary>Says once per type and failure what went wrong in a mod's drawing.</summary>
  public void Failed(ElementType type, string doing, Exception exception)
  {
    string key = type.FullKey + " " + doing;
    if (!_warned.Add(key))
      return;
    _warn(
      "["
        + type.ModId
        + "] Its "
        + type.EnglishName
        + " element stopped while "
        + doing
        + " ("
        + exception.GetType().Name
        + ": "
        + exception.Message
        + "). Element hid it; the rest of the overlay keeps going. Update the mod or tell its author.\n"
        + exception.StackTrace
    );
  }

  private void Rebuild()
  {
    _types.Clear();
    _offered.Clear();
    for (int i = 0; i < _mods.Length; i++)
    {
      if (!_active[i])
        continue;
      foreach (ElementType type in _mods[i].Elements)
      {
        if (type?.FullKey == null || _types.ContainsKey(type.FullKey))
          continue;
        _types[type.FullKey] = type;
        _offered.Add(type);
      }
    }
    // A reload brings new instances; their failures are new too.
    _warned.Clear();
  }

  private bool IsActive(AttachedMod mod)
  {
    try
    {
      return mod.IsActive();
    }
    catch (Exception exception)
    {
      if (_warned.Add(mod.Id + " active"))
        _warn("Couldn't tell whether " + mod.Id + " is on, so its elements are hidden. " + exception.Message);
      return false;
    }
  }
}
