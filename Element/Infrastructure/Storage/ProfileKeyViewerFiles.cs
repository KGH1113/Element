using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Element.Application.Profiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Element.Infrastructure.Storage;

/// <summary>
/// Profiles' key viewers in the mod root's Profiles folder, one ElementNote "Save all" file
/// per profile. Updates leave the mod root alone. Key viewers no profile keeps any more go
/// to Backups; ElementNote's "Load all" opens them.
/// </summary>
internal sealed class ProfileKeyViewerFiles : IProfileKeyViewerFiles
{
  public const string FolderName = "Profiles";
  private const string BackupPrefix = "KeyViewers-";
  private const string CountsField = "keyCounters";

  private readonly string _directory;
  private readonly string _backups;

  public ProfileKeyViewerFiles(string modRoot)
  {
    _directory = Path.Combine(Path.GetFullPath(modRoot), FolderName);
    _backups = Path.Combine(Path.GetFullPath(modRoot), SettingsBackup.FolderName);
  }

  public string PathOf(string profileId) => Path.Combine(_directory, FileStem(profileId) + ".json");

  public string ExportPathOf(string profileId) => Path.Combine(_directory, FileStem(profileId) + ".export.json");

  public string NewBackupPath()
  {
    Directory.CreateDirectory(_backups);
    string stamp = BackupPrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
    string path = Path.Combine(_backups, stamp + ".json");
    for (int copy = 2; File.Exists(path); copy++)
      path = Path.Combine(_backups, stamp + "-" + copy + ".json");
    Prune(keep: SettingsBackup.Kept - 1);
    return path;
  }

  public bool Exists(string profileId) => File.Exists(PathOf(profileId));

  public string Read(string path) => File.ReadAllText(path, Encoding.UTF8);

  // ElementNote keeps a waiting profile's press counts in its file; they stay on this PC.
  public string ReadToShare(string path)
  {
    string text = Read(path);
    if (!text.Contains("\"" + CountsField + "\""))
      return text;
    JObject viewers = JObject.Parse(text);
    viewers.Remove(CountsField);
    return viewers.ToString(Formatting.Indented);
  }

  public void Write(string profileId, string json)
  {
    Directory.CreateDirectory(_directory);
    string path = PathOf(profileId);
    string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.WriteAllText(temporary, json, new UTF8Encoding(false));
      if (File.Exists(path))
        File.Delete(path);
      File.Move(temporary, path);
      temporary = null;
    }
    finally
    {
      if (temporary != null && File.Exists(temporary))
        File.Delete(temporary);
    }
  }

  public void Copy(string fromProfileId, string toProfileId)
  {
    Directory.CreateDirectory(_directory);
    File.Copy(PathOf(fromProfileId), PathOf(toProfileId), true);
  }

  public void Delete(string path)
  {
    if (File.Exists(path))
      File.Delete(path);
  }

  public void BackUp(string profileId)
  {
    string path = PathOf(profileId);
    if (!File.Exists(path))
      return;
    File.Move(path, NewBackupPath());
  }

  private void Prune(int keep)
  {
    foreach (
      FileInfo old in new DirectoryInfo(_backups)
        .GetFiles(BackupPrefix + "*.json")
        .OrderByDescending(file => file.Name, StringComparer.Ordinal)
        .Skip(keep)
    )
      old.Delete();
  }

  // Profile ids are 32 hex digits; a hand-edited one still names a file in this folder.
  private static string FileStem(string profileId)
  {
    string id = profileId ?? "";
    if (id.Length > 0 && id.Length <= 64 && id.All(c => (c < 128 && char.IsLetterOrDigit(c)) || c == '-' || c == '_'))
      return id;
    using SHA1 hash = SHA1.Create();
    return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant();
  }
}
