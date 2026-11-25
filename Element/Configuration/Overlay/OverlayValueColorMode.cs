namespace Element.Configuration.Overlay;

// How a color that follows a value moves between its stops: blending
// smoothly from one to the next, or jumping to a stop's color the moment the
// value reaches it.
public enum OverlayValueColorMode
{
  Smooth,
  Step,
}
