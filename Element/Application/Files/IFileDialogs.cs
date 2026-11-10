using System;

namespace Element.Application.Files;

internal enum FileDialogStatus
{
  Chosen,
  Cancelled,

  /// <summary>The system file window could not open, such as when its native library is missing.</summary>
  Unavailable,
}

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

internal sealed class FileDialogRequest
{
  public string Title = "";

  /// <summary>The file extension offered and saved, without the dot.</summary>
  public string Extension = "";

  /// <summary>Open only: older extensions the same files may have.</summary>
  public string[] AlsoOpens = new string[0];

  /// <summary>Every extension an open window shows, the main one first.</summary>
  public string[] OpenExtensions()
  {
    string[] all = new string[1 + (AlsoOpens?.Length ?? 0)];
    all[0] = Extension ?? "";
    AlsoOpens?.CopyTo(all, 1);
    return all;
  }

  /// <summary>The name of that file type in the window's type list, such as "Element profile".</summary>
  public string TypeName = "";

  /// <summary>Save only: the suggested file name, extension included.</summary>
  public string FileName = "";
}

/// <summary>
/// The operating system's open and save windows. Both return once the player
/// picks a file or cancels; the game does not draw meanwhile.
/// </summary>
internal interface IFileDialogs
{
  FileDialogResult Open(FileDialogRequest request);

  FileDialogResult Save(FileDialogRequest request);
}
