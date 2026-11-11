namespace Element.Application.Files;

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
