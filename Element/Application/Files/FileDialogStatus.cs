namespace Element.Application.Files;

internal enum FileDialogStatus
{
  Chosen,
  Cancelled,

  /// <summary>The system file window could not open, such as when its native library is missing.</summary>
  Unavailable,
}
