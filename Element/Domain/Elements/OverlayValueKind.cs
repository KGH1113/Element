namespace Element.Domain.Elements;

// What a value is to arithmetic: a plain number, a time in seconds shown as
// m:ss, or words that can only be shown as they are.
internal enum OverlayValueKind
{
  Number,
  Time,
  Text,
}
