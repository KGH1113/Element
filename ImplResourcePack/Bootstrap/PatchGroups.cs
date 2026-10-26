using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ImplResourcePack.Bootstrap;

/// <summary>Names the feature a Harmony patch class belongs to when its namespace is too broad.</summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class PatchGroupAttribute : Attribute
{
  public PatchGroupAttribute(string name) => Name = name;

  public string Name { get; }
}

/// <summary>
/// Applies patches one feature at a time. A game update that renames one target
/// then disables only that feature instead of failing the whole mod load.
/// </summary>
internal static class PatchGroups
{
  public static IReadOnlyList<string> Apply(Harmony harmony, Assembly assembly, Action<string, Exception> logError)
  {
    var failed = new List<string>();
    IEnumerable<IGrouping<string, Type>> groups = AccessTools
      .GetTypesFromAssembly(assembly)
      .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0)
      .GroupBy(GroupOf)
      .OrderBy(group => group.Key, StringComparer.Ordinal);
    foreach (IGrouping<string, Type> group in groups)
    {
      var applied = new List<PatchClassProcessor>();
      try
      {
        foreach (Type type in group)
        {
          PatchClassProcessor processor = harmony.CreateClassProcessor(type);
          processor.Patch();
          applied.Add(processor);
        }
      }
      catch (Exception exception)
      {
        // A half-patched feature could block input or desync state; roll it back.
        foreach (PatchClassProcessor processor in applied)
        {
          try
          {
            processor.Unpatch();
          }
          catch (Exception rollback)
          {
            logError("Roll back " + group.Key + " patches", rollback);
          }
        }
        failed.Add(group.Key);
        logError(group.Key + " is off because this game version changed a patched method", exception);
      }
    }
    return failed;
  }

  private static string GroupOf(Type type) =>
    type.GetCustomAttribute<PatchGroupAttribute>()?.Name ?? type.Namespace ?? type.Name;
}
