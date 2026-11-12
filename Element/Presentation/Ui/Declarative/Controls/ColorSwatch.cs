using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A button showing a color as a dot beside a caption; pressing it opens
/// Element's color picker below it (<see cref="ColorPickerView"/>), which
/// writes the color as the player picks. Clicking outside, Escape or the
/// picker's close button closes it. The button takes the width it is
/// offered: give it a <c>Frame(width:)</c> in a row.
/// </summary>
/// <example>
/// <code>
/// ColorSwatch.Of(L("공 색상", "Body color"), Bind(() =&gt; part.Color), opacity: true,
///     caption: Text(() =&gt; part.Appearance.Opacity, format: "{0}%", number: "0"))
/// </code>
/// </example>
internal static class ColorSwatch
{
  /// <param name="title">The picker's title, such as "Body color".</param>
  /// <param name="color">The color it shows and the picker writes.</param>
  /// <param name="opacity">Whether the picker edits the alpha too.</param>
  /// <param name="caption">Beside the dot, such as the opacity; null shows the HEX code.</param>
  /// <param name="defaultColor">Without opacity, the picker's Default button puts this color back; null shows none.</param>
  public static View Of(
    string title,
    Binding<Color> color,
    bool opacity = false,
    View caption = null,
    Color? defaultColor = null
  )
  {
    SwatchModel swatch = new();
    Binding<bool> open = Bind(() => swatch.Open);
    return Button(
        HStack(
          spacing: 8,
          Spacer().Frame(width: 16, height: 16).Background(() => Opaque(color.Value), 8),
          (caption ?? Text(() => Hex(color.Value))).Frame(maxWidth: float.PositiveInfinity)
        ),
        () => swatch.Show
      )
      .Popover(open, () => ColorPickerView.Of(title, color, opacity, defaultColor, open));
  }

  /// <summary>Whether the picker is open.</summary>
  private sealed class SwatchModel : ViewModel
  {
    public bool Open;

    public SwatchModel() => Show = Command(() => Open = true);

    public Command Show { get; }
  }

  [Pure]
  private static UiColor Opaque(Color color) => new Color(color.r, color.g, color.b, 1);

  [Pure]
  private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);
}
