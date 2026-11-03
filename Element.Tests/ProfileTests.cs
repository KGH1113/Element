using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using Element.Application.Files;
using Element.Application.Profiles;
using Element.Configuration;
using Element.Configuration.Effects;
using Element.Configuration.Profiles;
using Element.Domain.Input;
using Element.Infrastructure.Profiles;

namespace Element.Tests;

internal static class ProfileTests
{
  public static void Run()
  {
    CopyKeepsHeldObjects();
    FirstProfileHoldsCurrentSettings();
    SwitchingKeepsEachProfilesValues();
    ProfilesSurviveTheSettingsFile();
    ExportedFilesImportUnderAFreeName();
    FilesThatAreNotProfilesAreRefused();
    DeletingTheProfileInUseSwitchesFirst();
    NamesAreCheckedLikePresetNames();
    RepairKeepsOneProfileInUse();
    ServiceAsksForFilesAndBacksUp();
    BackupsKeepTheNewest();
  }

  // Settings windows and controllers hold nested objects; a copy must change
  // their values, not replace them, and carry every saved field.
  private static void CopyKeepsHeldObjects()
  {
    GameplayTweaksSettings live = new();
    GameplayTweaksSettings incoming = new();
    incoming.Editor.ShowAngle = true;
    incoming.Editor.RotationStep = 7.5f;
    incoming.Judgement.HideMiss = true;
    incoming.Planets.Enabled = true;
    incoming.Planets.Red.Body.ColorHex = "#010203";
    incoming.Planets.Blue.Ring.Opacity = 40;
    PlanetAppearanceProfile red = live.Planets.Red;
    EditorTweaksOptions editor = live.Editor;
    SettingsCopy.Into(live, incoming);
    Check(Xml(live) == Xml(incoming), "every gameplay value is copied");
    Check(live.Planets.Red == red && live.Editor == editor, "nested objects stay the ones already held");

    EffectSettings effects = new();
    EffectSettings other = new();
    Check(other.TryAdd("Mine", out _, out _), "a preset can be added");
    other.Enabled = true;
    List<EffectPreset> presets = effects.Presets;
    SettingsCopy.Into(effects, other);
    Check(Xml(effects) == Xml(other), "every effect value is copied");
    Check(
      effects.Presets == presets && effects.Presets.Count == other.Presets.Count,
      "the preset list stays the same list"
    );
  }

  private static void FirstProfileHoldsCurrentSettings()
  {
    ElementSettings settings = Fresh();
    string before = Values(settings);
    ProfileLibrary library = new(settings, "기본");
    Check(library.Profiles.Count == 1, "settings from before profiles become one profile");
    Check(
      library.Active?.Name == "기본" && library.Active.Content == null,
      "it is in use and named in the game's language"
    );
    Check(Values(settings) == before, "making the first profile changes nothing");
    ProfileSummary summary = library.Summarize(library.Active);
    Check(
      summary.Elements == settings.OverlayLayouts.Count && summary.Presets == settings.Effects.Presets.Count,
      "the profile in use is summarized from the live settings"
    );
  }

  private static void SwitchingKeepsEachProfilesValues()
  {
    ElementSettings settings = Fresh();
    settings.Gameplay.Chatter.WindowMs = ChatterFilter.MaximumWindowMs;
    settings.OverlayLayouts.Add(SpacedText("spaces"));
    ProfileLibrary library = new(settings, "Default");
    ElementProfile first = library.Active;
    string original = Values(settings);
    GameplayTweaksSettings gameplay = settings.Gameplay;
    PlanetAppearanceProfile red = settings.Gameplay.Planets.Red;
    EffectSettings effects = settings.Effects;
    List<EffectPreset> presets = settings.Effects.Presets;
    List<OverlayLayoutSettings> layouts = settings.OverlayLayouts;

    Check(library.TryCreate("Stream", out ElementProfile stream, out _), "a new profile is created");
    Check(library.Active == stream && first.Content != null, "the new profile is in use; the first keeps its values");
    Check(Values(settings) == original, "a new profile starts as a copy");

    settings.HighlightColor = "#FF0000";
    settings.OverlayFont = "Pretendard|Bold";
    settings.Gameplay.Planets.Red.Body.ColorHex = "#00FF00";
    settings.Gameplay.Judgement.HideMiss = true;
    settings.Gameplay.Editor.FineRotation = true;
    settings.Effects.Enabled = true;
    Check(settings.Effects.TryAdd("Stream only", out _, out _), "a preset can be added");
    settings.OverlayLayouts.RemoveAt(0);
    settings.Gameplay.Chatter.WindowMs = ChatterFilter.MinimumWindowMs;
    string streamValues = Values(settings);

    Check(library.Switch(first.Id), "switching back works");
    Check(Values(settings) == original, "the first profile's values come back exactly");
    Check(
      settings.Gameplay.Chatter.WindowMs == ChatterFilter.MinimumWindowMs,
      "the chatter filter is shared, not switched"
    );
    Check(
      settings.Gameplay == gameplay
        && settings.Gameplay.Planets.Red == red
        && settings.Effects == effects
        && settings.Effects.Presets == presets
        && settings.OverlayLayouts == layouts,
      "switching changes values inside the objects the mod holds"
    );
    Check(
      settings.OverlayLayouts.Find(l => l.Id == "spaces")?.Text.Runs[1].Text == "  ",
      "a text of only spaces survives a switch"
    );
    Check(first.Content == null && stream.Content != null, "only the profile in use has no stored values");

    Check(library.Switch(stream.Id), "switching forward works");
    Check(Values(settings) == streamValues, "the second profile's values come back exactly");
    Check(!library.Switch(stream.Id), "switching to the profile in use does nothing");
  }

  private static void ProfilesSurviveTheSettingsFile()
  {
    ElementSettings settings = Fresh();
    settings.OverlayLayouts.Add(SpacedText("spaces"));
    ProfileLibrary library = new(settings, "Default");
    string first = Values(settings);
    library.TryCreate("Other", out _, out _);
    settings.OverlayLayouts.Clear();

    using MemoryStream file = new();
    new XmlSerializer(typeof(ElementSettings)).Serialize(file, settings);
    file.Position = 0;
    ElementSettings loaded = SettingsXml.Read<ElementSettings>(file);
    loaded.Migrate();
    ProfileLibrary reloaded = new(loaded, "Default");
    Check(reloaded.Profiles.Count == 2 && reloaded.Active.Name == "Other", "profiles and the one in use load back");
    Check(reloaded.Switch(reloaded.Profiles[0].Id), "a loaded profile switches");
    Check(Values(loaded) == first, "a stored profile keeps its values, spaces included, through the settings file");
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

    string directory = TempDirectory();
    try
    {
      string large = Path.Combine(directory, "large." + ProfileFile.Extension);
      using (FileStream file = File.Create(large))
        file.SetLength(ProfileFileCodec.MaximumBytes + 1);
      Check(
        ProfileFileCodec.ReadFile(large).Problem == ProfileFileProblem.TooLarge,
        "a huge file is not read into memory"
      );
      Check(
        ProfileFileCodec.ReadFile(Path.Combine(directory, "missing")).Problem == ProfileFileProblem.ReadFailed,
        "a missing file reports a read failure"
      );
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  private static void DeletingTheProfileInUseSwitchesFirst()
  {
    ElementSettings settings = Fresh();
    ProfileLibrary library = new(settings, "Default");
    ElementProfile first = library.Active;
    string firstValues = Values(settings);
    library.TryCreate("Second", out ElementProfile second, out _);
    settings.HighlightColor = "#00FF00";

    Check(library.Delete(second.Id, out bool switched) && switched, "deleting the profile in use switches away first");
    Check(library.Active == first && library.Profiles.Count == 1, "its neighbor is in use");
    Check(Values(settings) == firstValues, "the neighbor's values are live");
    Check(!library.CanDelete && !library.Delete(first.Id, out _), "the only profile is never deleted");
  }

  private static void NamesAreCheckedLikePresetNames()
  {
    ProfileLibrary library = new(Fresh(), "Default");
    Check(
      !library.ValidateName("   ", null, out _, out ProfileNameProblem empty) && empty == ProfileNameProblem.Empty,
      "an empty name is refused"
    );
    Check(
      !library.ValidateName(
        new string('a', ElementProfile.MaximumNameLength + 1),
        null,
        out _,
        out ProfileNameProblem tooLong
      )
        && tooLong == ProfileNameProblem.TooLong,
      "a long name is refused"
    );
    Check(
      !library.ValidateName(" default ", null, out _, out ProfileNameProblem taken)
        && taken == ProfileNameProblem.Taken,
      "names ignore case and spaces"
    );
    Check(
      library.ValidateName("default", library.Active.Id, out string same, out _) && same == "default",
      "a profile may keep its own name"
    );
    Check(
      library.TryRename(library.Active.Id, "  Practice ", out _) && library.Active.Name == "Practice",
      "renaming trims"
    );
  }

  private static void RepairKeepsOneProfileInUse()
  {
    ElementSettings settings = Fresh();
    ProfileContent stored = ProfileContent.Capture(settings);
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "a",
        Name = "Same",
        Content = stored,
      }
    );
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "a",
        Name = "Duplicate id",
        Content = stored,
      }
    );
    settings.Profiles.Add(
      new ElementProfile
      {
        Id = "b",
        Name = "same",
        Content = stored,
      }
    );
    settings.Profiles.Add(new ElementProfile { Id = "c", Name = "Lost values" });
    settings.Profiles.Add(null);
    settings.ActiveProfileId = "missing";
    ProfileLibrary library = new(settings, "Default");
    Check(library.Profiles.Count == 3, "broken entries are dropped: " + library.Profiles.Count);
    Check(
      library.Active != null && library.Active.Content == null && library.Active.Name == "Default",
      "the live settings get a profile"
    );
    Check(library.Profiles[1].Name == "Same" && library.Profiles[2].Name == "same 2", "names are made unique in order");

    ElementSettings inUse = Fresh();
    inUse.Profiles.Add(
      new ElementProfile
      {
        Id = "x",
        Name = "X",
        Content = ProfileContent.Capture(inUse),
      }
    );
    inUse.ActiveProfileId = "x";
    ProfileLibrary repaired = new(inUse, "Default");
    Check(
      repaired.Profiles.Count == 1 && repaired.Active.Content == null,
      "the profile in use stores nothing; the live settings win"
    );
  }

  private static void ServiceAsksForFilesAndBacksUp()
  {
    string directory = TempDirectory();
    try
    {
      ElementSettings settings = Fresh();
      FakeDialogs dialogs = new();
      int backups = 0,
        reloads = 0,
        saves = 0;
      ProfileService service = new(
        new ProfileLibrary(settings, "Default"),
        dialogs,
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
      ProfileOutcome exported = service.Export(service.Active.Id, "Export");
      Check(
        exported.Ok && exported.Detail == "stream." + ProfileFile.Extension,
        "the extension is added: " + exported.Detail
      );
      Check(
        dialogs.Last.FileName == "Stream_ main_." + ProfileFile.Extension,
        "the suggested name is safe on every system: " + dialogs.Last.FileName
      );
      Check(File.Exists(Path.Combine(directory, "stream." + ProfileFile.Extension)), "the file is written");
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
        ProfileService.WithExtension("a." + ProfileFile.Extension + "." + ProfileFile.Extension)
          == "a." + ProfileFile.Extension
          && ProfileService.WithExtension("a." + ProfileFile.Extension.ToUpperInvariant())
            == "a." + ProfileFile.Extension.ToUpperInvariant(),
        "a name the save window gave the extension twice is saved with it once"
      );
      Check(
        service.Delete(service.Active.Id).Ok && backups == 2 && reloads == 2,
        "deleting the profile in use backs up and applies its neighbor"
      );

      dialogs.Throw = true;
      Check(
        service.Import("Import").Problem == ProfileProblem.DialogUnavailable,
        "a file window that cannot open is reported"
      );

      ProfileService noFiles = new(new ProfileLibrary(Fresh(), "Default"), null, null, null, null, "", null);
      Check(
        !noFiles.FilesSupported && noFiles.Import("Import").Problem == ProfileProblem.DialogUnavailable,
        "no file windows, no import"
      );
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  private static void BackupsKeepTheNewest()
  {
    string directory = TempDirectory();
    try
    {
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
    finally
    {
      Directory.Delete(directory, true);
    }
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

  private static ElementSettings Fresh()
  {
    ElementSettings settings = new();
    settings.Migrate();
    settings.Gameplay.Normalize();
    settings.Effects.Normalize();
    return settings;
  }

  private static OverlayLayoutSettings SpacedText(string id)
  {
    OverlayLayoutSettings layout = new()
    {
      Id = id,
      TemplateId = OverlayElementKinds.Text,
      Customized = true,
      Text = new OverlayTextSettings(),
    };
    layout.Text.Runs.Add(OverlayTextRun.ValueRun("fps", OverlayColorSettings.HighlightColor()));
    layout.Text.Runs.Add(OverlayTextRun.Literal("  ", OverlayColorSettings.Custom("#FFFFFF")));
    layout.Text.Runs.Add(OverlayTextRun.ValueRun("bpm", OverlayColorSettings.HighlightColor()));
    return layout;
  }

  // Everything a profile carries, as the settings file would hold it.
  private static string Values(ElementSettings settings) => Xml(ProfileContent.Capture(settings));

  private static string Xml(object value)
  {
    using StringWriter writer = new();
    new XmlSerializer(value.GetType()).Serialize(writer, value);
    return writer.ToString();
  }

  private static ProfileFileResult Read(string xml) =>
    ProfileFileCodec.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

  private static void Refused(string xml, ProfileFileProblem expected, string what)
  {
    ProfileFileProblem actual = Read(xml).Problem;
    Check(actual == expected, what + " is refused as " + expected + ", got " + actual);
  }

  private static string TempDirectory()
  {
    string path = Path.Combine(Path.GetTempPath(), "element-profile-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
  }

  private static void Check(bool condition, string message)
  {
    if (!condition)
      throw new InvalidOperationException(message);
  }
}
