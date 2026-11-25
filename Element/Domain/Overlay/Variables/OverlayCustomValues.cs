using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Variables;

/// <summary>
/// The values the player made, shared by every element: each a name, the
/// type it gives and a variable, found by key. Set from the settings when
/// they load or a made value changes (<see cref="Load"/>), which also lists
/// them in the value catalog; <see cref="Version"/> goes up so what shows
/// them can follow. Main thread only.
/// </summary>
internal static class OverlayCustomValues
{
  private static readonly Dictionary<string, (string Name, OverlayValueType Type, OverlayVariable Variable)> ByKey =
    new();
  private static readonly List<string> Order = new();
  private static readonly List<(string Key, string Name, OverlayValueKind Kind)> Listed = new();

  /// <summary>Goes up each time the made values change.</summary>
  public static int Version { get; private set; }

  /// <summary>The keys in the order the player made them.</summary>
  public static IReadOnlyList<string> Keys => Order;

  /// <summary>Takes the made values as saved, and lists them under "My values".</summary>
  public static void Load(IReadOnlyList<OverlayCustomValueSettings> values)
  {
    ByKey.Clear();
    Order.Clear();
    Listed.Clear();
    if (values != null)
      foreach (OverlayCustomValueSettings value in values)
      {
        string key = value?.Key;
        if (string.IsNullOrEmpty(key) || ByKey.ContainsKey(key))
          continue;
        OverlayValueType type = ParseType(value.Type);
        string name = value.Name ?? string.Empty;
        ByKey[key] = (name, type, OverlayVariable.Parse(value.Variable));
        Order.Add(key);
        Listed.Add((key, name, KindOf(type)));
      }
    OverlayValueCatalog.SetMine(Listed);
    Version++;
  }

  public static bool TryGet(string key, out OverlayVariable variable)
  {
    variable = null;
    if (key == null || !ByKey.TryGetValue(key, out (string Name, OverlayValueType Type, OverlayVariable Variable) made))
      return false;
    variable = made.Variable;
    return true;
  }

  /// <summary>The name the player gave it, empty while it has none; null for a key nobody made.</summary>
  public static string NameOf(string key) =>
    key != null && ByKey.TryGetValue(key, out (string Name, OverlayValueType Type, OverlayVariable Variable) made)
      ? made.Name
      : null;

  /// <summary>Its name to show: the player's, or what <paramref name="words"/> say for none or a value nobody made.</summary>
  public static string Shown(string key, OverlayVariableWords words)
  {
    string name = NameOf(key);
    return name == null ? words.Custom
      : string.IsNullOrWhiteSpace(name) ? words.Unnamed
      : name;
  }

  /// <summary>What it gives, as it was made; Any for a key nobody made.</summary>
  public static OverlayValueType TypeOf(string key) =>
    key != null && ByKey.TryGetValue(key, out (string Name, OverlayValueType Type, OverlayVariable Variable) made)
      ? made.Type
      : OverlayValueType.Any;

  /// <summary>A type as the settings save it.</summary>
  public static string TypeName(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => "number",
      OverlayValueType.Text => "text",
      OverlayValueType.Color => "color",
      OverlayValueType.Bool => "bool",
      _ => "any",
    };

  private static OverlayValueType ParseType(string name) =>
    name switch
    {
      "number" => OverlayValueType.Number,
      "text" => OverlayValueType.Text,
      "color" => OverlayValueType.Color,
      "bool" => OverlayValueType.Bool,
      _ => OverlayValueType.Any,
    };

  private static OverlayValueKind KindOf(OverlayValueType type) =>
    type switch
    {
      OverlayValueType.Number => OverlayValueKind.Number,
      OverlayValueType.Text => OverlayValueKind.Text,
      OverlayValueType.Color => OverlayValueKind.Color,
      OverlayValueType.Bool => OverlayValueKind.Bool,
      _ => OverlayValueKind.Any,
    };
}
