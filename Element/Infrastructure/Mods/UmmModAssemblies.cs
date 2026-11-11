using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System;
using Element.Application.Mods;
using UnityModManagerNet;

namespace Element.Infrastructure.Mods;

/// <summary>
/// The UMM mods built against ElementKit. A mod that loads later, reloads or
/// unloads changes the version; checking costs a pass over UMM's mod list.
/// </summary>
internal sealed class UmmModAssemblies : IModAssemblies
{
  private readonly List<ModAssembly> _mods = new();
  private readonly Dictionary<Assembly, bool> _usesKit = new();
  private int _signature = int.MinValue;
  private int _version;

  public int Version
  {
    get
    {
      Update();
      return _version;
    }
  }

  public IReadOnlyList<ModAssembly> Current
  {
    get
    {
      Update();
      return _mods;
    }
  }

  public List<ElementClass> FindElements(ModAssembly mod, Action<string> report) =>
    ElementClasses.Find(mod.Id, mod.Assembly, report);

  private void Update()
  {
    List<UnityModManager.ModEntry> entries = UnityModManager.modEntries;
    if (entries == null)
      return;
    int signature = entries.Count;
    foreach (UnityModManager.ModEntry entry in entries)
      signature = unchecked(
        signature * 31 + (entry?.Assembly == null ? 0 : RuntimeHelpers.GetHashCode(entry.Assembly))
      );
    if (signature == _signature)
      return;
    _signature = signature;
    _mods.Clear();
    foreach (UnityModManager.ModEntry entry in entries)
    {
      Assembly assembly = entry?.Assembly;
      if (assembly == null || !UsesKit(assembly))
        continue;
      UnityModManager.ModEntry mod = entry;
      _mods.Add(new ModAssembly(mod.Info.Id, assembly, () => mod.Active));
    }
    _version++;
  }

  private bool UsesKit(Assembly assembly)
  {
    if (!_usesKit.TryGetValue(assembly, out bool uses))
      _usesKit[assembly] = uses = ElementClasses.UsesKit(assembly);
    return uses;
  }
}
