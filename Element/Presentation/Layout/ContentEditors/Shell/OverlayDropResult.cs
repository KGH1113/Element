namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>What became of a value dropped on a formula.</summary>
internal enum OverlayDropResult
{
  Applied,

  // Words such as the song title can't go in a number field.
  RefusedWords,
}
