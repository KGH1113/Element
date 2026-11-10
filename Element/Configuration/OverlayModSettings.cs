using System;
using System.Collections.Generic;

namespace Element.Configuration;

// An element another mod draws (TemplateId "ModId/type"): the properties the
// player set, by the keys that mod gave them. Properties the mod no longer
// has, or a mod that isn't installed, leave them as they are, so reinstalling
// brings the element back as it was.
[Serializable]
public sealed class OverlayModSettings
{
  // The size the element last drew at, in pixels at 1080p: the box players
  // see in the editor while the mod that draws it is missing. Not an edit, so
  // undo and the editor's change checks ignore it.
  public float Width;
  public float Height;

  // The type's names when it last drew, so the editor can still name the
  // element while its mod is missing. Not an edit either.
  public string KoreanName = string.Empty;
  public string EnglishName = string.Empty;

  public List<OverlayPropertySetting> Properties = new();

  public OverlayPropertySetting Find(string key)
  {
    if (Properties == null)
      return null;
    foreach (OverlayPropertySetting property in Properties)
      if (property != null && property.Key == key)
        return property;
    return null;
  }

  /// <summary>The property saved under <paramref name="key"/>, added when it has none yet.</summary>
  public OverlayPropertySetting Get(string key)
  {
    OverlayPropertySetting property = Find(key);
    if (property != null)
      return property;
    Properties ??= new List<OverlayPropertySetting>();
    property = new OverlayPropertySetting { Key = key };
    Properties.Add(property);
    return property;
  }

  public OverlayModSettings Clone()
  {
    OverlayModSettings copy = new()
    {
      Width = Width,
      Height = Height,
      KoreanName = KoreanName ?? string.Empty,
      EnglishName = EnglishName ?? string.Empty,
    };
    if (Properties != null)
      foreach (OverlayPropertySetting property in Properties)
        if (property != null)
          copy.Properties.Add(property.Clone());
    return copy;
  }

  /// <summary>Whether both hold the same properties, in any order.</summary>
  public bool SameAs(OverlayModSettings other)
  {
    if (other == null)
      return false;
    int count = 0;
    if (Properties != null)
      foreach (OverlayPropertySetting property in Properties)
      {
        if (property == null)
          continue;
        count++;
        if (!property.SameAs(other.Find(property.Key)))
          return false;
      }
    int otherCount = 0;
    if (other.Properties != null)
      foreach (OverlayPropertySetting property in other.Properties)
        if (property != null)
          otherCount++;
    return count == otherCount;
  }
}

// One property: a number (invariant culture) with its formula, a choice's
// key, "true"/"false" for a toggle, or a color.
[Serializable]
public sealed class OverlayPropertySetting
{
  public string Key = string.Empty;
  public string Value = string.Empty;
  public string Formula = string.Empty;
  public OverlayColorSettings Color;

  public OverlayPropertySetting Clone() =>
    new()
    {
      Key = Key,
      Value = Value ?? string.Empty,
      Formula = Formula ?? string.Empty,
      Color = Color?.Clone(),
    };

  public bool SameAs(OverlayPropertySetting other) =>
    other != null
    && Key == other.Key
    && (Value ?? string.Empty) == (other.Value ?? string.Empty)
    && OverlayColorSettings.SameFormula(Formula, other.Formula)
    && (Color?.SameAs(other.Color) ?? other.Color == null);
}
