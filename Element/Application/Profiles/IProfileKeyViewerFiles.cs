namespace Element.Application.Profiles;

/// <summary>
/// The files that hold the key viewers of profiles ElementNote doesn't show, each an
/// ElementNote "Save all" file. ElementNote reads and writes them by their full paths.
/// </summary>
internal interface IProfileKeyViewerFiles
{
  /// <summary>The file of a profile's own key viewers, whether it exists or not.</summary>
  string PathOf(string profileId);

  /// <summary>Where an export has ElementNote save the key viewers on screen.</summary>
  string ExportPathOf(string profileId);

  /// <summary>A new file in Backups, for key viewers no profile keeps any more.</summary>
  string NewBackupPath();

  bool Exists(string profileId);

  /// <summary>The text of a file named by one of the paths above.</summary>
  string Read(string path);

  /// <summary>A file's text for a profile file another player may import: without this PC's press counts.</summary>
  string ReadToShare(string path);

  void Write(string profileId, string json);
  void Copy(string fromProfileId, string toProfileId);

  /// <summary>Removes a file named by one of the paths above; a missing file is fine.</summary>
  void Delete(string path);

  /// <summary>Moves a profile's file into Backups, keeping the newest few there.</summary>
  void BackUp(string profileId);
}
