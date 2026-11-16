using System;
using System.IO;
using System.Text;
using Element.Application.Files;
using Element.Application.Profiles;
using Element.Configuration;
using Element.Configuration.Profiles;
using Element.Infrastructure.Storage;
using Element.Tests.Support;
using static Element.Tests.Application.Profiles.ProfileFixtures;
using static Element.Tests.Support.Assert;

namespace Element.Tests.Application.Profiles;

/// <summary>
/// Profile files: exported and imported under a free name, bad files refused, the
/// file windows asked, and settings backed up.
/// </summary>
internal static class ProfileFileTests
{
  internal static void Run()
  {
    ExportedFilesImportUnderAFreeName();
    FilesThatAreNotProfilesAreRefused();
    ServiceAsksForFilesAndBacksUp();
    BackupsKeepTheNewest();
  }

  private static void ExportedFilesImportUnderAFreeName()
  {
    ElementSettings source = Fresh();
    source.HighlightColor = "#123456";
    source.OverlayLayouts.Add(SpacedText("spaces"));
    source.Gameplay.Planets.Enabled = true;
    source.Effects.Presets[0].ShortcutKey = UnityEngine.KeyCode.F9;
    source.Effects.Presets[0].ShortcutModifier = UnityEngine.KeyCode.LeftAlt;
    ProfileLibrary sourceLibrary = new(source, "Default");
    ProfileFile exported = sourceLibrary.Export(sourceLibrary.Active.Id, "9.9.9");
    using MemoryStream stream = new();
    ProfileFileCodec.Write(stream, exported);
    string text = Encoding.UTF8.GetString(stream.ToArray());
    Check(
      text.Contains("<ElementProfile") && text.Contains("<ModVersion>9.9.9</ModVersion>"),
      "the file names itself and its version"
    );
    Check(!text.Contains("<Chatter>"), "the file leaves out this PC's chatter filter");
    stream.Position = 0;
    ProfileFileResult read = ProfileFileCodec.Read(stream);
    Check(read.Ok && read.File.Name == "Default", "the file reads back");

    ElementSettings target = Fresh();
    target.Gameplay.Chatter.WindowMs = 77;
    ProfileLibrary targetLibrary = new(target, "Default");
    ElementProfile imported = targetLibrary.Import(read.File);
    Check(imported.Name == "Default 2", "an import takes a free name: " + imported.Name);
    Check(targetLibrary.Active == imported && targetLibrary.Profiles.Count == 2, "the import is added and in use");
    Check(target.Effects.Presets[0].ShortcutKey == UnityEngine.KeyCode.F9, "a preset shortcut comes along");
    Check(Values(target) == Values(source), "the import brings every profile value");

    // A preset shortcut equal to this PC's settings shortcut would open settings and apply the preset at once.
    ElementSettings clash = Fresh();
    clash.SettingsShortcutKey = UnityEngine.KeyCode.F9;
    clash.SettingsShortcutModifier = UnityEngine.KeyCode.LeftAlt;
    new ProfileLibrary(clash, "Default").Import(read.File);
    Check(!clash.Effects.Presets[0].HasShortcut, "a preset shortcut that is this PC's settings shortcut is dropped");
    Check(target.Gameplay.Chatter.WindowMs == 77, "the import keeps this PC's chatter filter");
    Check(targetLibrary.Profiles[0].Content != null, "the profile that was in use keeps its values");
  }

  private static void FilesThatAreNotProfilesAreRefused()
  {
    Refused("Not XML at all", ProfileFileProblem.NotProfile, "plain text");
    Refused(
      "<Settings><HighlightColor>#FFFFFF</HighlightColor></Settings>",
      ProfileFileProblem.NotProfile,
      "another XML file"
    );
    Refused(
      "<!DOCTYPE ElementProfile [<!ENTITY big \"x\">]><ElementProfile><Name>&big;</Name></ElementProfile>",
      ProfileFileProblem.NotProfile,
      "a file with a DTD"
    );
    Refused(
      "<ElementProfile><Format>1</Format></ElementProfile>",
      ProfileFileProblem.Damaged,
      "a profile with no values"
    );
    Refused("<ElementProfile><Format>one</Format></ElementProfile>", ProfileFileProblem.Damaged, "a broken profile");
    ProfileFileResult newer = Read(
      "<ElementProfile><Format>2</Format><ModVersion>3.0.0</ModVersion><Content /></ElementProfile>"
    );
    Check(
      newer.Problem == ProfileFileProblem.NewerVersion && newer.File?.ModVersion == "3.0.0",
      "a newer file format is refused with its version"
    );
    ProfileFileResult migrated = Read(
      "<ElementProfile><Format>1</Format><Content><SettingsVersion>"
        + (ElementSettings.CurrentSettingsVersion + 1)
        + "</SettingsVersion></Content></ElementProfile>"
    );
    Check(migrated.Problem == ProfileFileProblem.NewerVersion, "values from newer settings are refused");

    using TempFolder folder = new("element-profile-tests-");
    string directory = folder.Path;
    string large = Path.Combine(directory, "large." + ProfileFile.Extension);
    using (FileStream file = File.Create(large))
      file.SetLength(ProfileFileStore.MaximumBytes + 1);
    Check(
      new ProfileFileStore().Read(large).Problem == ProfileFileProblem.TooLarge,
      "a huge file is not read into memory"
    );
    Check(
      new ProfileFileStore().Read(Path.Combine(directory, "missing")).Problem == ProfileFileProblem.ReadFailed,
      "a missing file reports a read failure"
    );
  }

  private static void ServiceAsksForFilesAndBacksUp()
  {
    using TempFolder folder = new("element-profile-tests-");
    string directory = folder.Path;
    ElementSettings settings = Fresh();
    FakeDialogs dialogs = new();
    int backups = 0,
      reloads = 0,
      saves = 0;
    ProfileService service = new(
      new ProfileLibrary(settings, "Default"),
      dialogs,
      new ProfileFileStore(),
      () =>
      {
        saves++;
        return true;
      },
      () => reloads++,
      () => backups++,
      "1.2.3",
      (_, _) => { }
    );
    Check(service.FilesSupported, "a platform with file windows offers import and export");
    service.Rename(service.Active.Id, "Stream: main?");

    dialogs.Next = FileDialogResult.Chosen(Path.Combine(directory, "stream"));
    ProfileOutcome exported = service.Export(service.Active.Id, "Export").Result;
    Check(
      exported.Ok && exported.Detail == "stream." + ProfileFile.Extension,
      "the extension is added: " + exported.Detail
    );
    Check(
      dialogs.Last.FileName == "Stream_ main_." + ProfileFile.Extension,
      "the suggested name is safe on every system: " + dialogs.Last.FileName
    );
    Check(File.Exists(Path.Combine(directory, "stream." + ProfileFile.Extension)), "the file is written");
    Check(
      ProfileService.WithExtension("a." + ProfileFile.Extension + "." + ProfileFile.Extension)
        == "a." + ProfileFile.Extension
        && ProfileService.WithExtension("a." + ProfileFile.Extension.ToUpperInvariant())
          == "a." + ProfileFile.Extension.ToUpperInvariant(),
      "a name the save window gave the extension twice is saved with it once"
    );
    Check(Directory.GetFiles(directory).Length == 1, "no temporary file is left behind");

    dialogs.Next = FileDialogResult.Cancelled;
    Check(
      service.Import("Import").Problem == ProfileProblem.Cancelled && backups == 0,
      "closing the window changes nothing"
    );

    string notProfile = Path.Combine(directory, "notes.txt");
    File.WriteAllText(notProfile, "hello");
    dialogs.Next = FileDialogResult.Chosen(notProfile);
    Check(
      service.Import("Import").Problem == ProfileProblem.NotProfile && backups == 0,
      "a wrong file is refused before any backup"
    );

    int savesBefore = saves;
    dialogs.Next = FileDialogResult.Chosen(Path.Combine(directory, "stream." + ProfileFile.Extension));
    ProfileOutcome imported = service.Import("Import");
    Check(
      imported.Ok && imported.Saved && imported.Name == "Stream: main? 2",
      "the import is named freely: " + imported.Name
    );
    Check(backups == 1 && reloads == 1 && saves == savesBefore + 1, "an import backs up, applies and saves once");
    Check(service.Active.Name == imported.Name && service.Profiles.Count == 2, "the import is in use");

    Check(
      service.Delete(service.Active.Id).Ok && backups == 2 && reloads == 2,
      "deleting the profile in use backs up and applies its neighbor"
    );

    dialogs.Throw = true;
    Check(
      service.Import("Import").Problem == ProfileProblem.DialogUnavailable,
      "a file window that cannot open is reported"
    );

    ProfileService noFiles = new(new ProfileLibrary(Fresh(), "Default"), null, null, null, null, null, "", null);
    Check(
      !noFiles.FilesSupported && noFiles.Import("Import").Problem == ProfileProblem.DialogUnavailable,
      "no file windows, no import"
    );
  }

  private static void BackupsKeepTheNewest()
  {
    using TempFolder folder = new("element-profile-tests-");
    string directory = folder.Path;
    ElementSettings settings = Fresh();
    new ProfileLibrary(settings, "Default");
    SettingsBackup backup = new(directory, () => settings);
    for (int i = 0; i < SettingsBackup.Kept + 3; i++)
      backup.Write();
    string[] files = Directory.GetFiles(Path.Combine(directory, SettingsBackup.FolderName));
    Check(files.Length == SettingsBackup.Kept, "only the newest backups are kept: " + files.Length);
    using FileStream file = File.OpenRead(files[0]);
    ElementSettings restored = SettingsXml.Read<ElementSettings>(file);
    Check(restored.Profiles.Count == 1, "a backup is a whole settings file");
  }

  private sealed class FakeDialogs : IFileDialogs
  {
    public FileDialogResult Next = FileDialogResult.Cancelled;
    public FileDialogRequest Last;
    public bool Throw;

    public FileDialogResult Open(FileDialogRequest request) => Show(request);

    public FileDialogResult Save(FileDialogRequest request) => Show(request);

    private FileDialogResult Show(FileDialogRequest request)
    {
      if (Throw)
        throw new DllNotFoundException("no file window");
      Last = request;
      return Next;
    }
  }

  private static ProfileFileResult Read(string xml) =>
    ProfileFileCodec.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

  private static void Refused(string xml, ProfileFileProblem expected, string what)
  {
    ProfileFileProblem actual = Read(xml).Problem;
    Check(actual == expected, what + " is refused as " + expected + ", got " + actual);
  }
}
