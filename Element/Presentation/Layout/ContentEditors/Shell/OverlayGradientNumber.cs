namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>A number of a gradient the input dock can type.</summary>
internal enum OverlayGradientNumber
{
  None,

  /// <summary>Where the picked color sits along the gradient, in percent.</summary>
  Position,

  /// <summary>Which way the gradient runs, in degrees.</summary>
  Angle,

  /// <summary>The picked color's opacity, in percent.</summary>
  Opacity,
}
