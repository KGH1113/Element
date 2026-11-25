namespace Element.Domain.Overlay.Conditions;

// How a condition's value compares to what it is held against. Unknown while
// either side has no data, as before a level starts.
internal enum OverlayComparison
{
  Unknown,
  Smaller,
  Same,
  Bigger,
}
