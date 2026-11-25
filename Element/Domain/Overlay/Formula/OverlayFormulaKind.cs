namespace Element.Domain.Overlay.Formula;

// What a place that takes a formula holds, which decides what may go in it:
// a number field takes numbers, a value in a text may also be words, and a
// color takes colors. Conditions compare numbers wherever they are.
internal enum OverlayFormulaKind
{
  Number,
  Text,
  Color,
}
