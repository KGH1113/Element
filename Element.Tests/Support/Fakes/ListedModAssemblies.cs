using System;
using System.Collections.Generic;
using Element.Application.Mods;
using Element.Infrastructure.Mods;

namespace Element.Tests.Support.Fakes;

/// <summary>A fixed list of mods, for tests.</summary>
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

  public List<ElementClass> FindElements(ModAssembly mod, Action<string> report) =>
    ElementClasses.Find(mod.Id, mod.Assembly, report);

  public void Remove(string id)
  {
    if (_mods.RemoveAll(mod => mod.Id == id) > 0)
      Version++;
  }
}
