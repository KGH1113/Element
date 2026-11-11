using System;
using System.Reflection;

namespace Element.Application.Mods;

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
