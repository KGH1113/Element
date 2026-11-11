using System;

namespace Element.Application.Files;

internal readonly struct FileDialogResult
{
  private FileDialogResult(FileDialogStatus status, string path, Exception exception)
  {
    Status = status;
    Path = path;
    Exception = exception;
  }

  public FileDialogStatus Status { get; }
  public string Path { get; }
  public Exception Exception { get; }

  public static FileDialogResult Chosen(string path) => new(FileDialogStatus.Chosen, path, null);

  public static readonly FileDialogResult Cancelled = new(FileDialogStatus.Cancelled, null, null);

  public static FileDialogResult Unavailable(Exception exception) => new(FileDialogStatus.Unavailable, null, exception);
}
