namespace Element.Presentation.OverlayEditor.Parts.Gradient;

/// <summary>A number of a gradient the input dock can type.</summary>
internal enum GradientNumber
{
  None,

  /// <summary>Where the picked color sits along the gradient, in percent.</summary>
  Position,

  /// <summary>Which way the gradient runs, in degrees.</summary>
  Angle,

  /// <summary>The picked color's opacity, in percent.</summary>
  Opacity,
}
