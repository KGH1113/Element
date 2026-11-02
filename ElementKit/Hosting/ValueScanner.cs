using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElementKit.Values;

namespace ElementKit.Hosting;

/// <summary>
/// Finds the value groups a mod keeps in static fields. Reading a field runs
/// its class's static initializer, so values exist before the mod first sets
/// them and the dock can offer them right away.
/// </summary>
internal static class ValueScanner
{
  private const BindingFlags StaticFields =
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

  public static List<ValueGroup> Find(Assembly assembly, Action<string> report)
  {
    HashSet<ValueGroup> found = new();
    foreach (Type type in LoadableTypes(assembly, report))
    {
      if (type.ContainsGenericParameters)
        continue;
      FieldInfo[] fields;
      try
      {
        fields = type.GetFields(StaticFields);
      }
      catch (Exception)
      {
        continue;
      }
      foreach (FieldInfo field in fields)
      {
        if (!typeof(ValueGroup).IsAssignableFrom(field.FieldType) && !typeof(Value).IsAssignableFrom(field.FieldType))
          continue;
        object value;
        try
        {
          value = field.GetValue(null);
        }
        catch (Exception exception)
        {
          // Reading the field wraps the initializer's own exception, often twice.
          Exception cause = exception;
          while (cause is TargetInvocationException or TypeInitializationException && cause.InnerException != null)
            cause = cause.InnerException;
          report(
            "[ElementKit] "
              + type.FullName
              + " stopped while making its values ("
              + cause.GetType().Name
              + ": "
              + cause.Message
              + "). Its values were skipped; the rest of the mod still attached."
          );
          break;
        }
        if (value is ValueGroup group)
          found.Add(group);
        else if (value is Value single && single.Group != null)
          found.Add(single.Group);
      }
    }
    return found.OrderBy(group => group.Order).ToList();
  }

  private static IEnumerable<Type> LoadableTypes(Assembly assembly, Action<string> report)
  {
    if (assembly == null)
      return Array.Empty<Type>();
    try
    {
      return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException exception)
    {
      report("[ElementKit] Some types in " + assembly.GetName().Name + " couldn't load; values in them were skipped.");
      return exception.Types.Where(type => type != null);
    }
  }
}
