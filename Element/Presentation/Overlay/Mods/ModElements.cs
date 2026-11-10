using System;
using System.Collections.Generic;
using System.Reflection;
using Element.Configuration;
using ElementKit.Hosting;

namespace Element.Presentation.Overlay.Mods;

/// <summary>A loaded mod that may declare overlay elements.</summary>
internal sealed class ModAssembly
{
  public ModAssembly(string id, Assembly assembly, Func<bool> isActive)
  {
    Id = id;
    Assembly = assembly;
    IsActive = isActive ?? (() => true);
  }

  public string Id { get; }
  public Assembly Assembly { get; }
  public Func<bool> IsActive { get; }
}

/// <summary>The mods that may declare elements: UMM's, or given directly in tests and the UI preview.</summary>
internal interface IModAssemblies
{
  /// <summary>Changes when a mod loads, unloads or reloads; reading it is cheap.</summary>
  int Version { get; }

  IReadOnlyList<ModAssembly> Current { get; }
}

/// <summary>
/// The overlay elements other mods declare, by the key layouts save
/// (<c>ModId/key</c>). Element finds them itself, so a mod needs no call and
/// no requirement on Element. Looked up again when a mod loads or reloads; a
/// turned-off mod's elements step aside until it is on again.
/// </summary>
internal sealed class ModElements
{
  private readonly IModAssemblies _source;
  private readonly Action<string> _warn;
  private readonly Dictionary<Assembly, List<ElementClass>> _scanned = new();
  private readonly Dictionary<string, ElementClass> _classes = new(StringComparer.Ordinal);
  private readonly List<ElementClass> _offered = new();
  private readonly HashSet<string> _warned = new(StringComparer.Ordinal);
  private IReadOnlyList<ModAssembly> _mods = Array.Empty<ModAssembly>();
  private bool[] _active = Array.Empty<bool>();
  private int _sourceVersion = -1;

  public ModElements(IModAssemblies source, Action<string> warn)
  {
    _source = source;
    _warn = warn ?? (_ => { });
  }

  /// <summary>Advances whenever an element kind comes or goes, so placed elements look theirs up again.</summary>
  public int Version { get; private set; }

  /// <summary>What players can add now, mod by mod.</summary>
  public IReadOnlyList<ElementClass> Offered => _offered;

  /// <summary>Before elements refresh: a comparison per mod unless a mod loaded or was turned on or off.</summary>
  public void Refresh()
  {
    if (_source == null)
      return;
    bool changed = false;
    int version = _source.Version;
    if (version != _sourceVersion)
    {
      _sourceVersion = version;
      _mods = _source.Current;
      _active = new bool[_mods.Count];
      for (int i = 0; i < _mods.Count; i++)
        _active[i] = IsActive(_mods[i]);
      changed = true;
    }
    else
      for (int i = 0; i < _mods.Count; i++)
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

  /// <summary>The kind <paramref name="templateId"/> names; null while its mod is missing or off.</summary>
  public ElementClass Find(string templateId) =>
    templateId != null && _classes.TryGetValue(templateId, out ElementClass type) ? type : null;

  /// <summary>Whether the mod that draws <paramref name="templateId"/> is installed but turned off.</summary>
  public bool IsTurnedOff(string templateId)
  {
    string mod = OverlayElementKinds.ModOf(templateId);
    for (int i = 0; i < _mods.Count; i++)
      if (_mods[i].Id == mod)
        return !_active[i];
    return false;
  }

  /// <summary>Says once per kind and failure what went wrong in a mod's element.</summary>
  public void Failed(ElementClass type, string doing, Exception exception)
  {
    if (type == null || exception == null || !_warned.Add(type.FullKey + " " + doing))
      return;
    _warn(
      "["
        + type.ModId
        + "] Its \""
        + type.Info.EnglishName
        + "\" element stopped while "
        + doing
        + " ("
        + exception.GetType().Name
        + ": "
        + exception.Message
        + "). Element paused it; the rest of the overlay keeps going.\n"
        + exception.StackTrace
    );
  }

  private void Rebuild()
  {
    _classes.Clear();
    _offered.Clear();
    for (int i = 0; i < _mods.Count; i++)
    {
      if (!_active[i])
        continue;
      foreach (ElementClass type in Scan(_mods[i]))
        if (_classes.ContainsKey(type.FullKey) == false)
        {
          _classes[type.FullKey] = type;
          _offered.Add(type);
        }
    }
    // A reload brings new classes; their failures are new too.
    _warned.Clear();
  }

  // Each assembly is read once; a reload is a new assembly.
  private List<ElementClass> Scan(ModAssembly mod)
  {
    if (mod.Assembly == null)
      return new List<ElementClass>();
    if (_scanned.TryGetValue(mod.Assembly, out List<ElementClass> found))
      return found;
    found = ElementClasses.Find(mod.Id, mod.Assembly, message => _warn("[" + mod.Id + "] " + message));
    _scanned[mod.Assembly] = found;
    return found;
  }

  private bool IsActive(ModAssembly mod)
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

/// <summary>A fixed list of mods, for tests and the UI preview.</summary>
internal sealed class ListedModAssemblies : IModAssemblies
{
  private readonly List<ModAssembly> _mods = new();

  public int Version { get; private set; }

  public IReadOnlyList<ModAssembly> Current => _mods;

  public void Add(ModAssembly mod)
  {
    _mods.Add(mod);
    Version++;
  }

  public void Remove(string id)
  {
    if (_mods.RemoveAll(mod => mod.Id == id) > 0)
      Version++;
  }
}
