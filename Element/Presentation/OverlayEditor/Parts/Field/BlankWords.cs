using Element.Presentation.Ui.Declarative.Controls;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Field;

/// <summary>
/// Words not typed yet, wherever a value is drawn: a hollow box about one
/// letter wide where the letters would be, in the outline of an empty place.
/// One look for it everywhere: the text preview keeps the same box in the
/// value's place, and the pills on the line, in the condition's fork and
/// strip, and in the property column draw it after their mark.
/// </summary>
internal static class BlankWords
{
  /// <summary>Its outline: an empty place's.</summary>
  public static readonly Color Edge = FieldLook.SlotEdge.Literal;

  /// <summary>How thick its outline is, in units.</summary>
  public const float EdgeWidth = 1.5f;

  /// <summary>The box, as large as one letter of words written at <paramref name="size"/>.</summary>
  public static View Box(float size)
  {
    float height = Mathf.Round(size * .82f),
      width = Mathf.Round(size * 1.1f);
    return RoundedRectangle(Mathf.Round(size * .26f))
      .Stroke(Edge, EdgeWidth)
      .Named("Blank words")
      .Frame(width: width, height: height);
  }
}
