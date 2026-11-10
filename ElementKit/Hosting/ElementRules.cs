using System;
using System.Collections.Generic;
using System.Linq;
using ElementKit.Elements;

namespace ElementKit.Hosting;

/// <summary>
/// The rules that keep other mods' elements as easy as Element's own: plain
/// names in both languages, keys layouts can keep, numbers with sensible
/// limits, and an editing screen that fits. A broken property is left out of
/// its type; a broken type can't be added. Each problem says how to fix it.
/// </summary>
internal static class ElementRules
{
  public static void Validate(IReadOnlyList<ElementType> types, Action<string> report)
  {
    HashSet<string> keys = new(StringComparer.Ordinal);
    foreach (ElementType type in types)
    {
      type.Accepted = Accept(type, keys, report);
      if (type.Accepted)
        keys.Add(type.Key);
    }
  }

  private static bool Accept(ElementType type, HashSet<string> keys, Action<string> report)
  {
    string name = "The element type \"" + type.Key + "\"";
    if (!KitRegistry.ValidKey(type.Key))
    {
      report(
        "[ElementKit] The element type key \""
          + type.Key
          + "\" can't be saved in layouts. Start it with a lowercase letter and use lowercase letters, digits and dashes, such as \"hand-bars\". The type was skipped."
      );
      return false;
    }
    if (keys.Contains(type.Key))
    {
      report(
        "[ElementKit] Two element types use the key \""
          + type.Key
          + "\". Give each its own key; the second one was skipped."
      );
      return false;
    }
    if (!Named(type.KoreanName, type.EnglishName))
    {
      report(
        "[ElementKit] "
          + name
          + " needs a short Korean and English name, such as \"손 막대\" and \"Hand bars\". The type was skipped."
      );
      return false;
    }
    HashSet<string> propertyKeys = new(StringComparer.Ordinal);
    List<Property> accepted = new();
    foreach (Property property in type.Properties)
      if (AcceptProperty(type, property, propertyKeys, report))
      {
        propertyKeys.Add(property.Key);
        accepted.Add(property);
      }
    type.Properties = accepted;
    EditorLayout layout = type.Layout ?? new EditorLayout();
    type.Layout = layout;
    CheckLayout(type, layout, accepted, report);
    return true;
  }

  private static bool AcceptProperty(ElementType type, Property property, HashSet<string> keys, Action<string> report)
  {
    string name = "The property \"" + property.Key + "\" of " + type.Key;
    if (!KitRegistry.ValidKey(property.Key))
    {
      report(
        "[ElementKit] "
          + name
          + " can't be saved. Use lowercase letters, digits and dashes, starting with a letter, such as \"left-color\". It was left out."
      );
      return false;
    }
    if (keys.Contains(property.Key))
    {
      report(
        "[ElementKit] "
          + type.Key
          + " has two properties with the key \""
          + property.Key
          + "\". The second one was left out."
      );
      return false;
    }
    if (!Named(property.KoreanName, property.EnglishName))
    {
      report(
        "[ElementKit] "
          + name
          + " needs a short Korean and English name, such as \"너비\" and \"Width\". It was left out."
      );
      return false;
    }
    switch (property)
    {
      case NumberProperty number:
        if (!Finite(number.Min) || !Finite(number.Max) || number.Min >= number.Max)
        {
          report(
            "[ElementKit] "
              + name
              + " needs a smallest number below its largest, such as min: 1, max: 1920. It was left out."
          );
          return false;
        }
        if (!Finite(number.Default) || number.Default < number.Min || number.Default > number.Max)
        {
          report(
            "[ElementKit] "
              + name
              + " starts outside its limits. Pick a starting number between min and max. It was left out."
          );
          return false;
        }
        return true;
      case ColorProperty color:
        if (!color.StartsWithHighlight && !IsHex(color.DefaultColor))
        {
          report(
            "[ElementKit] "
              + name
              + " needs a color like \"#FF9F43\" or ColorProperty.Highlight to start with. It was left out."
          );
          return false;
        }
        return true;
      case ChoiceProperty choice:
        return AcceptChoice(name, choice, report);
      case ToggleProperty:
        return true;
      default:
        report(
          "[ElementKit] " + name + " is a kind of property this Element doesn't know. Update Element; it was left out."
        );
        return false;
    }
  }

  private static bool AcceptChoice(string name, ChoiceProperty choice, Action<string> report)
  {
    if (choice.Options.Count < 2)
    {
      report("[ElementKit] " + name + " needs two or more options to choose from. It was left out.");
      return false;
    }
    HashSet<string> keys = new(StringComparer.Ordinal);
    foreach (ChoiceOption option in choice.Options)
    {
      if (option == null || !KitRegistry.ValidKey(option.Key) || !keys.Add(option.Key))
      {
        report(
          "[ElementKit] "
            + name
            + " has an option without its own key. Give each option a key of lowercase letters, digits and dashes. It was left out."
        );
        return false;
      }
      if (!Named(option.KoreanName, option.EnglishName))
      {
        report(
          "[ElementKit] "
            + name
            + "'s option \""
            + option.Key
            + "\" needs a Korean and an English name. It was left out."
        );
        return false;
      }
    }
    return true;
  }

  // The screen holds each accepted property once and a bar that fits.
  private static void CheckLayout(ElementType type, EditorLayout layout, List<Property> accepted, Action<string> report)
  {
    HashSet<Property> own = new(accepted);
    foreach (Property property in layout.All().Where(property => !own.Contains(property)).Distinct().ToList())
      report(
        "[ElementKit] "
          + type.Key
          + "'s editing screen shows \""
          + property.Key
          + "\", which isn't one of its accepted properties. Keep properties in static readonly fields of the type; it was left off the screen."
      );
    layout.Keep(own);
    List<Property> shown = layout.All().ToList();
    if (shown.Count != shown.Distinct().Count())
      report("[ElementKit] " + type.Key + "'s editing screen shows a property twice. Players see it in each place.");
    if (layout.BarItems.Count > EditorLayout.MaximumBar)
      report(
        "[ElementKit] "
          + type.Key
          + "'s bar has "
          + layout.BarItems.Count
          + " properties; it fits "
          + EditorLayout.MaximumBar
          + ". Only the first "
          + EditorLayout.MaximumBar
          + " show. Move numbers Below or Beside the element."
      );
    foreach (Property property in accepted.Where(property => !shown.Contains(property)))
      report(
        "[ElementKit] "
          + type.Key
          + "'s property \""
          + property.Key
          + "\" isn't on its editing screen, so players can't change it. Put it in Edit with editor.Bar, Below or Beside."
      );
  }

  private static bool Named(string korean, string english) =>
    !string.IsNullOrWhiteSpace(korean) && !string.IsNullOrWhiteSpace(english);

  private static bool Finite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);

  internal static bool IsHex(string color)
  {
    if (string.IsNullOrEmpty(color) || color.Length != 7 || color[0] != '#')
      return false;
    for (int i = 1; i < 7; i++)
      if (!Uri.IsHexDigit(color[i]))
        return false;
    return true;
  }
}
