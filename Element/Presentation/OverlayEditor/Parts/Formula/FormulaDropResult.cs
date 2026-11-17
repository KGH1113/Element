namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>What became of a value dropped on a formula.</summary>
internal enum FormulaDropResult
{
  Applied,

  // Words such as the song title can't go in a number field.
  RefusedWords,
}
