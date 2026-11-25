namespace Element.Presentation.OverlayEditor.Parts;

/// <summary>
/// What a full-screen editor's parts ask of it, and the only way up from
/// them: a part changes what it holds, then asks for the screen to be drawn
/// again (<see cref="Render"/>), for what is open to close, or for a notice.
/// What they read from it goes the other way, through <see cref="IEditorContext"/>.
/// </summary>
internal interface IEditorActions
{
  /// <summary>Something changed: everything shown is drawn again from what is edited.</summary>
  void Render();

  /// <summary>Closes the number or value being typed and the popovers.</summary>
  void CloseOpen();

  /// <summary>Esc: closes the picker or what is open, or with nothing open finishes like Done.</summary>
  void Escape();

  /// <summary>Done (<paramref name="commit"/>) or Cancel.</summary>
  void Close(bool commit, bool notify);

  /// <summary>A short message for a few seconds, on the dock's line or in the header; a warning reads warm.</summary>
  void Notice(string message, bool warning = true);
}
