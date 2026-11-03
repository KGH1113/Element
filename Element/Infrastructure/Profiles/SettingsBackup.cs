using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Element.Configuration;

namespace Element.Infrastructure.Profiles;

/// <summary>
/// Copies the settings, every profile included, into Backups at the mod root
/// before a profile import or delete. Updates leave the mod root alone. The
/// newest few copies are kept; replacing Settings.xml with one restores it.
/// </summary>
internal sealed class SettingsBackup
{
  public const string FolderName = "Backups";
  public const int Kept = 10;
  private const string Prefix = "Settings-";

  private readonly string _directory;
  private readonly Func<ElementSettings> _settings;

  public SettingsBackup(string modRoot, Func<ElementSettings> settings)
  {
    _directory = Path.Combine(modRoot, FolderName);
    _settings = settings;
  }

  public void Write()
  {
    Directory.CreateDirectory(_directory);
    string stamp = Prefix + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
    string path = Path.Combine(_directory, stamp + ".xml");
    for (int copy = 2; File.Exists(path); copy++)
      path = Path.Combine(_directory, stamp + "-" + copy + ".xml");
    using (FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write))
    using (
      XmlWriter writer = XmlWriter.Create(
        stream,
        new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }
      )
    )
      new XmlSerializer(typeof(ElementSettings)).Serialize(writer, _settings());
    foreach (
      FileInfo old in new DirectoryInfo(_directory)
        .GetFiles(Prefix + "*.xml")
        .OrderByDescending(file => file.Name, StringComparer.Ordinal)
        .Skip(Kept)
    )
      old.Delete();
  }
}
