namespace Element.Application.KeyViewer;

internal enum KeyViewerEditResult
{
  Opened,
  AppClosed,

  /// <summary>The running ElementNote predates editing from the game.</summary>
  AppOutdated,
  Failed,
}
