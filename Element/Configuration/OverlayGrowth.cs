namespace Element.Configuration;

// The point of an element that stays put when its content changes size, such
// as a shape whose width follows a value; it grows away from that point. Auto
// uses the screen anchor, picked from where the element sits.
public enum OverlayGrowth
{
  Auto = -1,
  TopLeft,
  Top,
  TopRight,
  Left,
  Center,
  Right,
  BottomLeft,
  Bottom,
  BottomRight,
}
