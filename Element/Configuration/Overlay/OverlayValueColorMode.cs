namespace Element.Configuration.Overlay;

// How a color that followed a value (OverlayValueColor, from earlier
// versions) moved between its stops: blending smoothly from one to the next,
// or jumping to a stop's color the moment the value reached it.
public enum OverlayValueColorMode
{
  Smooth,
  Step,
}
