using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Elements;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// One piece as a field row shows what it holds: a small chip with its type's
/// mark (or its color, or a gradient), what it says, or an operator's sign
/// between chips.
/// </summary>
internal sealed class FieldChip
{
  public FieldChip(string key, string label, OverlayValueType type)
  {
    Key = key;
    Label = label;
    Type = type;
  }

  public readonly string Key;

  /// <summary>
  /// Its place and what it shows: a chip view is drawn once, so a chip that
  /// says something else is a new row.
  /// </summary>
  public string Identity =>
    Key
    + "\u0001"
    + Label
    + "\u0001"
    + Glyph
    + (Blank ? "\u0001blank" : "")
    + (Digits.Length > 0 ? "\u0001" + Digits : "")
    + (HasSwatch ? "\u0001" + ColorUtility.ToHtmlStringRGB(Swatch) : "");

  public readonly string Label;

  public readonly OverlayValueType Type;

  /// <summary>Letters in place of the type's mark: "Aa" for a font, "⊙" for an anchor.</summary>
  public string Glyph;

  /// <summary>A color: its swatch leads instead of a mark.</summary>
  public bool HasSwatch;

  public Color Swatch = Color.white;

  /// <summary>A gradient's swatch; null for none.</summary>
  public OverlayResolvedGradient Gradient;

  /// <summary>An operator's sign, drawn plain between chips.</summary>
  public bool Operator;

  /// <summary>Words in place of a value, such as "비어 있음", muted.</summary>
  public bool Muted;

  /// <summary>Only its words, as a font's name or an anchor's point: the row's mark already says what it is.</summary>
  public bool Bare;

  /// <summary>Words not typed yet: a hollow box stands where they would be (<see cref="BlankWords"/>).</summary>
  public bool Blank;

  /// <summary>A value read at decimals of its own: how many, as "0.0", said quietly after it; empty for its own.</summary>
  public string Digits = "";
}
