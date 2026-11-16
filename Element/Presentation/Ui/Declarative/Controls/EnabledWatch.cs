using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Nothing to see: follows whether the view it stands in takes input, so a
/// row of Element's own can read muted while the screen around it turns it
/// off with <c>.Disabled(...)</c>, as ElementUI's controls do. Put it beside
/// the row's words and read <see cref="Enabled"/> in their color.
/// </summary>
internal sealed class EnabledWatch : NativeView
{
  /// <summary>Whether the view takes input now.</summary>
  public readonly State<bool> Enabled = new(true);

  protected override void Build(RectTransform rect) => Enabled.Value = IsEnabled;

  protected override void EnabledChanged(bool enabled) => Enabled.Value = enabled;

  protected override Size PreferredSize(Proposal proposal) => Size.Zero;
}
