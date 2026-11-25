namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>What became of a value dropped on a formula.</summary>
internal enum FormulaDropResult
{
  Applied,

  // It can't go there: words in a number, a color in a number, a number
  // where a case's color goes. FormulaRefusals says why.
  Refused,
}
