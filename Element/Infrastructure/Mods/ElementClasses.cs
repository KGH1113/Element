using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Element.Application.Mods;
using ElementKit;
using ElementKit.Hosting;

namespace Element.Infrastructure.Mods;

/// <summary>
/// Finds the overlay elements a mod declares: concrete <see cref="OverlayElement"/>
/// classes marked <see cref="ElementAttribute"/>. Nothing in the mod runs; a
/// class with a problem is left out and the problem explained.
/// </summary>
internal static class ElementClasses
{
  public static List<ElementClass> Find(string modId, Assembly assembly, Action<string> report)
  {
    report ??= _ => { };
    List<ElementClass> found = new();
    if (!KitRegistry.ValidModId(modId))
      return found;
    HashSet<string> keys = new(StringComparer.Ordinal);
    foreach (
      Type type in ValueScanner.LoadableTypes(assembly, report).OrderBy(type => type.FullName, StringComparer.Ordinal)
    )
    {
      if (!typeof(OverlayElement).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
        continue;
      ElementAttribute info = type.GetCustomAttribute<ElementAttribute>(false);
      if (info == null)
      {
        report(
          "[ElementKit] "
            + type.FullName
            + " is an OverlayElement without [Element(\"key\", \"이름\", \"Name\")], so players can't add it. Mark it to offer it."
        );
        continue;
      }
      string name = "The element " + type.FullName;
      if (!KitRegistry.ValidKey(info.Key))
      {
        report(
          "[ElementKit] "
            + name
            + " has the key \""
            + info.Key
            + "\", which layouts can't keep. Use lowercase letters, digits and dashes, starting with a letter, such as \"hand-bars\". It was left out."
        );
        continue;
      }
      if (!keys.Add(info.Key))
      {
        report(
          "[ElementKit] Two elements of "
            + modId
            + " use the key \""
            + info.Key
            + "\". Give each its own key; "
            + type.FullName
            + " was left out."
        );
        continue;
      }
      if (string.IsNullOrWhiteSpace(info.KoreanName) || string.IsNullOrWhiteSpace(info.EnglishName))
      {
        report(
          "[ElementKit] "
            + name
            + " needs a short Korean and English name, such as \"진행 막대\" and \"Progress bars\". It was left out."
        );
        continue;
      }
      if (
        type.GetConstructor(
          BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
          null,
          Type.EmptyTypes,
          null
        ) == null
      )
      {
        report(
          "[ElementKit] "
            + name
            + " needs a constructor that takes nothing; Element makes one per placed element. It was left out."
        );
        continue;
      }
      found.Add(new ElementClass(modId, type, info));
    }
    return found;
  }

  /// <summary>Whether <paramref name="assembly"/> was built against ElementKit, so it may declare elements.</summary>
  public static bool UsesKit(Assembly assembly)
  {
    if (assembly == null)
      return false;
    try
    {
      return assembly.GetReferencedAssemblies().Any(name => name.Name == "ElementKit");
    }
    catch (Exception)
    {
      return false;
    }
  }
}
