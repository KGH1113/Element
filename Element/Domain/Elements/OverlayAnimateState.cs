namespace Element.Domain.Elements;

// Whether a range's values animate. A range of only literal text has nothing
// that could.
internal enum OverlayAnimateState
{
  NoValues,
  Off,
  On,
  Mixed,
}
