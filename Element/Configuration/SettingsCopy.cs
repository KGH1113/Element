using System;
using System.Collections;
using System.Reflection;

namespace Element.Configuration;

/// <summary>
/// Copies saved values into settings objects the mod already holds. The
/// settings window, the layout canvas and the gameplay tweaks keep references
/// to these objects, so a profile switch changes their values in place rather
/// than handing out new objects. Every public field is copied, so a field added
/// later travels with profiles without touching this class.
/// </summary>
internal static class SettingsCopy
{
  public static void Into(object target, object source)
  {
    if (target == null)
      throw new ArgumentNullException(nameof(target));
    if (source == null)
      throw new ArgumentNullException(nameof(source));
    Type type = target.GetType();
    if (source.GetType() != type)
      throw new ArgumentException("Cannot copy " + source.GetType().Name + " into " + type.Name);

    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
      if (field.IsInitOnly)
        continue;
      object value = field.GetValue(source);
      object current = field.GetValue(target);
      if (value != null && current != null && IsSettingsObject(field.FieldType))
        Into(current, value);
      else if (value is IList items && current is IList list && !field.FieldType.IsArray)
      {
        list.Clear();
        foreach (object item in items)
          list.Add(item);
      }
      else
        field.SetValue(target, value);
    }
  }

  private static bool IsSettingsObject(Type type) =>
    type.IsClass
    && type != typeof(string)
    && !typeof(IEnumerable).IsAssignableFrom(type)
    && type.Namespace != null
    && type.Namespace.StartsWith("Element.Configuration", StringComparison.Ordinal);
}
