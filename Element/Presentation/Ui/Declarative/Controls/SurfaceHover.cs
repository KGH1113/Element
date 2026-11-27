using ElementUI;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// The light a button shows under the pointer when the surface a person sees
/// is drawn around it (a card, a field's box, a chip's outline) rather than by
/// the button: the button fills that surface, and its light takes the
/// surface's corners. A plain button would light a pill of its own instead.
/// </summary>
internal static class SurfaceHover
{
  /// <summary>The light itself: the same as a plain button's.</summary>
  public static readonly UiColor Light = new Color(1, 1, 1, .08f);

  private static readonly UiColor Pressed = new Color(0, 0, 0, .12f);

  /// <summary>Clear at rest; lit under the pointer, with <paramref name="radius"/> corners like the surface around it.</summary>
  public static ButtonSurfaceStyle Of(float radius) =>
    new(Color.clear, radius, hovered: Light, pressed: Pressed, disabled: Color.clear);
}
