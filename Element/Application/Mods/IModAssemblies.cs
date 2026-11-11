using System;
using System.Collections.Generic;

namespace Element.Application.Mods;

/// <summary>The mods that may declare elements: UMM's, or given directly in tests and the UI preview.</summary>
internal interface IModAssemblies
{
  /// <summary>Changes when a mod loads, unloads or reloads; reading it is cheap.</summary>
  int Version { get; }

  IReadOnlyList<ModAssembly> Current { get; }

  /// <summary>
  /// The element classes <paramref name="mod"/> declares. Nothing in the mod runs; a class
  /// with a problem is left out and the problem told to <paramref name="report"/>.
  /// </summary>
  List<ElementClass> FindElements(ModAssembly mod, Action<string> report);
}
