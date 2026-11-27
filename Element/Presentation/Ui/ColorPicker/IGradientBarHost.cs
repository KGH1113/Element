using Element.Configuration.Overlay;
using UnityEngine;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>What a <see cref="UiGradientBar"/> needs from the picker it sits in.</summary>
internal interface IGradientBarHost
{
  /// <summary>The bar shows: the picker is an overlay editor's.</summary>
  bool GradientShown { get; }

  /// <summary>Every color follows the highlight color; only opacities differ.</summary>
  bool FollowsHighlight { get; }

  /// <summary>The color the picker's controls show.</summary>
  Color Value { get; }

  /// <summary>Shows <paramref name="color"/> in the picker's controls without reporting a change.</summary>
  void ShowColor(Color color);

  /// <summary>Lays the picker out again after the bar changed size.</summary>
  void Relayout();

  /// <summary>Tells the picker's owner the color changed.</summary>
  void Report(OverlayColorSettings value);

  /// <summary>The color stopped following the highlight color (it became a gradient, which is always of custom colors).</summary>
  void LeftHighlight();
}
