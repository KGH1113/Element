namespace Element.Application.Files;

/// <summary>
/// The operating system's open and save windows. Both return once the player
/// picks a file or cancels; the game does not draw meanwhile.
/// </summary>
internal interface IFileDialogs
{
  FileDialogResult Open(FileDialogRequest request);

  FileDialogResult Save(FileDialogRequest request);
}
