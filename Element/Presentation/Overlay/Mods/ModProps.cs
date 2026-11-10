using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using ElementKit.Elements;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// A mod element's properties as its mod reads them while drawing: saved
/// numbers with their formulas worked out on live values (or samples in the
/// editors), colors ready for <see cref="ModSurface"/>, choices and toggles.
/// Formulas are parsed once per change of the saved properties.
/// </summary>
internal sealed class ModProps : Props
{
  private sealed class Slot
  {
    public double Number;
    public OverlayFormula Formula;
    public int Choice;
    public bool Toggle;
    public ModPaint Paint;
  }

  private readonly Dictionary<Property, Slot> _slots = new(ByReference.Instance);
  private IOverlayNumberSource _source;
  private bool _preview;

  /// <summary>The values the properties use; the element redraws when one changes.</summary>
  public OverlayValueMask Mask { get; private set; }

  public void Compile(ElementType type, OverlayModSettings settings)
  {
    _slots.Clear();
    Mask = OverlayValueMask.None;
    if (type == null)
      return;
    foreach (Property property in type.Properties)
    {
      OverlayPropertySetting saved = settings?.Find(property.Key);
      Slot slot = new();
      switch (property)
      {
        case NumberProperty number:
          slot.Number = ModPropertyValues.Number(number, saved);
          slot.Formula = OverlayFormulaText.Compile(saved?.Formula);
          Mask |= OverlayTextComposer.Mask(slot.Formula);
          break;
        case ColorProperty color:
          slot.Paint = new ModPaint(ModPropertyValues.Color(color, saved));
          Mask |= slot.Paint.Mask;
          break;
        case ChoiceProperty choice:
          slot.Choice = ModPropertyValues.Choice(choice, saved);
          break;
        case ToggleProperty toggle:
          slot.Toggle = ModPropertyValues.Toggle(toggle, saved);
          break;
      }
      _slots[property] = slot;
    }
  }

  /// <summary>Where values come from for the next draw, and the highlight color to draw with.</summary>
  public void Use(IOverlayNumberSource source, bool preview, Color highlight)
  {
    _source = source;
    _preview = preview;
    foreach (Slot slot in _slots.Values)
      slot.Paint?.Use(source, highlight);
  }

  public override double this[NumberProperty property]
  {
    get
    {
      if (property == null)
        return 0;
      if (!_slots.TryGetValue(property, out Slot slot))
        return property.Default;
      return ModPropertyValues.Clamp(property, OverlayFormulaText.Resolve(slot.Formula, _source, slot.Number));
    }
  }

  public override ElementColor this[ColorProperty property]
  {
    get
    {
      if (property != null && _slots.TryGetValue(property, out Slot slot) && slot.Paint != null)
        return new ElementColor(slot.Paint.Solid, slot.Paint);
      return ElementColor.From(Color.white);
    }
  }

  public override int this[ChoiceProperty property] =>
    property != null && _slots.TryGetValue(property, out Slot slot) ? slot.Choice : 0;

  public override bool this[ToggleProperty property] =>
    property != null && (_slots.TryGetValue(property, out Slot slot) ? slot.Toggle : property.Default);

  public override bool IsPreview => _preview;

  // Properties are compared as the objects the mod declared, never by value.
  private sealed class ByReference : IEqualityComparer<Property>
  {
    public static readonly ByReference Instance = new();

    public bool Equals(Property x, Property y) => ReferenceEquals(x, y);

    public int GetHashCode(Property obj) => RuntimeHelpers.GetHashCode(obj);
  }
}

/// <summary>
/// A picked color as a mod element draws it: the highlight or a custom
/// color, its opacity worked out on values, and a gradient resolved for
/// whatever box it fills.
/// </summary>
internal sealed class ModPaint
{
  private readonly OverlayColorSettings _color;
  private readonly OverlayFormula _alpha;
  private readonly OverlayGradientFormulas _gradient = new();
  private IOverlayNumberSource _source;
  private Color _highlight = Color.white;

  public ModPaint(OverlayColorSettings color)
  {
    _color = color;
    _alpha = OverlayFormulaText.Compile(color?.AlphaFormula);
    _gradient.Compile(color);
    Mask = OverlayTextComposer.Mask(_alpha) | _gradient.Mask;
  }

  public OverlayValueMask Mask { get; }

  public void Use(IOverlayNumberSource source, Color highlight)
  {
    _source = source;
    _highlight = highlight;
  }

  /// <summary>One color: a gradient shows its first.</summary>
  public Color Solid => OverlayTextComposer.Resolve(_color, _highlight, _alpha, _source);

  /// <summary>The gradient worked out for a <paramref name="width"/> by <paramref name="height"/> box; false for a solid color.</summary>
  public bool TryGradient(float width, float height, OverlayResolvedGradient output) =>
    _gradient.Resolve(_color, _highlight, _source, width, height, output);
}
