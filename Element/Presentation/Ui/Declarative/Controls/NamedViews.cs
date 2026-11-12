using ElementUI;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>Names for the Unity objects behind views.</summary>
internal static class NamedViews
{
  /// <summary>
  /// Names the GameObject that draws <paramref name="view"/> (a control, a text,
  /// an icon), so the panel around the screen and the UI preview can find it:
  /// <c>Button(...).Named("Add element")</c>. Stacks and spacers draw nothing and can't be named.
  /// </summary>
  public static View Named(this View view, string name) => view.Configure<GameObject>(target => target.name = name);
}
