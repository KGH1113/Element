using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Element.Configuration;
using Element.Configuration.Profiles;
using Element.Tests.Support;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Configuration;

/// <summary>
/// Profile switches fill settings objects in place only when their class is
/// marked. A saved class without the mark would be replaced instead, and
/// whatever held the old object would stop seeing changes.
/// </summary>
internal static class SettingsCopyTests
{
  [Suite("profile switches fill every saved settings class in place")]
  public static void Run()
  {
    SavedClassesAreMarked();
    ConfigurationClassesWithFieldsAreMarked();
    MarkedClassesAreSavedSettings();
  }

  // Every class a settings file or a profile file holds, wherever it lives.
  private static void SavedClassesAreMarked()
  {
    foreach (Type type in Saved())
      Check(IsMarked(type), type.FullName + " is saved but not copied in place");
  }

  // The rule before the mark: any class in Element.Configuration with fields to copy.
  private static void ConfigurationClassesWithFieldsAreMarked()
  {
    foreach (Type type in typeof(ElementSettings).Assembly.GetTypes())
      if (MatchedByNamespace(type) && HasCopiedFields(type))
        Check(IsMarked(type), type.FullName + " lost its in-place copy");
  }

  private static void MarkedClassesAreSavedSettings()
  {
    HashSet<Type> saved = Saved();
    foreach (Type type in typeof(ElementSettings).Assembly.GetTypes())
      if (IsMarked(type))
        Check(saved.Contains(type), type.FullName + " is marked but no settings file holds it");
  }

  private static HashSet<Type> Saved()
  {
    HashSet<Type> found = new();
    Queue<Type> pending = new();
    pending.Enqueue(typeof(ElementSettings));
    pending.Enqueue(typeof(ProfileFile));
    while (pending.Count > 0)
    {
      Type type = pending.Dequeue();
      if (!found.Add(type))
        continue;
      foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
      foreach (Type held in Held(field.FieldType))
        pending.Enqueue(held);
    }
    return found;
  }

  // The settings classes a field holds: itself, or the items of a list or array.
  private static IEnumerable<Type> Held(Type type)
  {
    if (type.IsArray)
      type = type.GetElementType();
    else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
      type = type.GetGenericArguments()[0];
    if (type.IsClass && type != typeof(string) && type.Assembly == typeof(ElementSettings).Assembly)
      yield return type;
  }

  private static bool MatchedByNamespace(Type type) =>
    type.IsClass
    && type != typeof(string)
    && !typeof(IEnumerable).IsAssignableFrom(type)
    && !type.IsDefined(typeof(CompilerGeneratedAttribute), false)
    && type.Namespace != null
    && type.Namespace.StartsWith("Element.Configuration", StringComparison.Ordinal);

  private static bool HasCopiedFields(Type type)
  {
    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
      if (!field.IsInitOnly)
        return true;
    return false;
  }

  private static bool IsMarked(Type type) => type.IsDefined(typeof(CopiedInPlaceAttribute), false);
}
