using Element.Configuration.Overlay;
using UnityEngine;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// What a color picker's model asks of the color bar over its controls
/// (<see cref="UiGradientBar"/>): the working color, solid or a gradient, and
/// its picked color, which the picker's controls edit.
/// </summary>
internal interface IGradientBar
{
  /// <summary>The color is a gradient, shown on the bar.</summary>
  bool IsGradient { get; }

  /// <summary>The picked color's place among the colors in order, counting from 1.</summary>
  int PickedRank { get; }

  /// <summary>Takes the color an editor shows; <paramref name="keepStop"/> stays on the picked color when it still exists.</summary>
  void Load(OverlayColorSettings working, bool keepStop);

  /// <summary>Takes the color again after the editor changed it, keeping the picked color.</summary>
  void Reload(OverlayColorSettings working);

  /// <summary>No overlay color is being edited: the picker shows a plain color.</summary>
  void Clear();

  /// <summary>The picked color, or the solid color, as the controls show it.</summary>
  Color WorkingColor();

  /// <summary>The color as the editor gets it after the controls moved to <paramref name="color"/>.</summary>
  OverlayColorSettings TakeColor(Color color, float alphaBefore);

  /// <summary>Makes every color follow the highlight, or each its own color again.</summary>
  void SetHighlight(bool highlight);

  /// <summary>Tells the picker's owner the working color changed.</summary>
  void Emit();

  /// <summary>Redraws the bar from the working color.</summary>
  void Refresh();

  /// <summary>Lays the bar out again, after what shows changed.</summary>
  void Relayout();
}
