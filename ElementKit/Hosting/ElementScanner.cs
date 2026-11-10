using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ElementKit.Elements;

namespace ElementKit.Hosting;

/// <summary>
/// Finds the element types a mod declares: every concrete <see cref="ElementType"/>
/// with a constructor that takes nothing. Each gets one instance, the
/// properties in its static fields, and the editing screen its Edit lays out.
/// A type that fails is skipped; the rest of the mod still attaches.
/// </summary>
internal static class ElementScanner
{
  private const BindingFlags StaticFields =
    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

  public static List<ElementType> Find(Assembly assembly, Action<string> report)
  {
    List<ElementType> found = new();
    foreach (Type type in ValueScanner.LoadableTypes(assembly, report))
    {
      if (type.IsAbstract || type.ContainsGenericParameters || !typeof(ElementType).IsAssignableFrom(type))
        continue;
      ElementType element = Create(type, report);
      if (element == null)
        continue;
      if (!TryReadProperties(type, report, out List<Property> properties))
        continue;
      element.Properties = properties;
      EditorLayout layout = new();
      try
      {
        element.RunEdit(layout);
      }
      catch (Exception exception)
      {
        report(Stopped(type, "laying out its editing screen", exception) + " The type was skipped.");
        continue;
      }
      element.Layout = layout;
      found.Add(element);
    }
    return found;
  }

  // The instance the type keeps in a static field of its own, so the mod can
  // call Redraw on it; otherwise a new one.
  private static ElementType Create(Type type, Action<string> report)
  {
    try
    {
      foreach (FieldInfo field in type.GetFields(StaticFields))
        if (
          type.IsAssignableFrom(field.FieldType)
          && field.GetValue(null) is ElementType kept
          && kept.GetType() == type
        )
          return kept;
    }
    catch (Exception exception)
    {
      report(Stopped(type, "being made", exception) + " The type was skipped.");
      return null;
    }
    ConstructorInfo constructor = type.GetConstructor(
      BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
      null,
      Type.EmptyTypes,
      null
    );
    if (constructor == null)
    {
      report(
        "[ElementKit] "
          + type.FullName
          + " needs a constructor that takes nothing, such as public MyElement() : base(\"my-element\", \"이름\", \"Name\") { }. The type was skipped."
      );
      return null;
    }
    try
    {
      return (ElementType)constructor.Invoke(null);
    }
    catch (Exception exception)
    {
      report(Stopped(type, "being made", exception) + " The type was skipped.");
      return null;
    }
  }

  // The properties in the type's static fields and its bases', in the order
  // they are written: base classes first, then each class top to bottom.
  private static bool TryReadProperties(Type type, Action<string> report, out List<Property> properties)
  {
    properties = new List<Property>();
    Stack<Type> chain = new();
    for (Type at = type; at != null && at != typeof(ElementType); at = at.BaseType)
      chain.Push(at);
    HashSet<Property> seen = new(ReferenceComparer.Instance);
    while (chain.Count > 0)
    {
      Type at = chain.Pop();
      FieldInfo[] fields;
      try
      {
        fields = at.GetFields(StaticFields);
      }
      catch (Exception)
      {
        continue;
      }
      foreach (FieldInfo field in fields.OrderBy(field => field.MetadataToken))
      {
        if (!typeof(Property).IsAssignableFrom(field.FieldType))
          continue;
        object value;
        try
        {
          value = field.GetValue(null);
        }
        catch (Exception exception)
        {
          report(Stopped(at, "making its properties", exception) + " Its element type was skipped.");
          return false;
        }
        if (value is Property property && seen.Add(property))
          properties.Add(property);
      }
    }
    return true;
  }

  private static string Stopped(Type type, string doing, Exception exception)
  {
    // Reflection wraps the type's own exception, often twice.
    Exception cause = exception;
    while (cause is TargetInvocationException or TypeInitializationException && cause.InnerException != null)
      cause = cause.InnerException;
    return "[ElementKit] "
      + type.FullName
      + " stopped while "
      + doing
      + " ("
      + cause.GetType().Name
      + ": "
      + cause.Message
      + ").";
  }

  private sealed class ReferenceComparer : IEqualityComparer<Property>
  {
    public static readonly ReferenceComparer Instance = new();

    public bool Equals(Property x, Property y) => ReferenceEquals(x, y);

    public int GetHashCode(Property obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
  }
}
