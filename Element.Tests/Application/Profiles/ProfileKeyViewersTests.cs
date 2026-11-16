using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
/// Each profile's ElementNote key viewers: switched with the profile, started fresh or copied,
/// kept in files while another profile is in use, exported and imported with the profile, and
/// never filed under the wrong profile.
/// </summary>
internal static class ProfileKeyViewersTests
{
  internal static void Run()
  {
    ProfilesBeforeKeyViewersKeepTheOnesOnScreen();
    NewProfilesStartFreshAndSwitchingBringsEachBack();
    DuplicatesAndImportsBringKeyViewers();
    ExportsCarryTheKeyViewers();
    DeletedKeyViewersGoToBackups();
    AStaleOwnerIsNeverOverwritten();
    WithoutElementNoteNothingIsLost();
  }

  private static void ProfilesBeforeKeyViewersKeepTheOnesOnScreen()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    Check(rig.Note.TrueOwner == rig.Service.Active.Id, "the key viewers on screen become the profile in use's");
    Check(rig.Note.Screen == "mine" && rig.Note.Swaps == 1, "nothing is loaded: " + rig.Note.Screen);

    ElementProfile other = rig.AddOldProfile("Old");
    rig.Service.Switch(other.Id);
    rig.Tick();
    Check(
      rig.Note.Owner == other.Id && rig.Note.Screen == "mine",
      "a profile from before starts with the ones on screen"
    );
    Check(rig.FileOf(rig.Library.Profiles[0]) == "mine", "the profile left keeps its own in a file");
  }

  private static void NewProfilesStartFreshAndSwitchingBringsEachBack()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    string first = rig.Service.Active.Id;
    rig.Service.Create("Stream");
    rig.Tick();
    string second = rig.Service.Active.Id;
    Check(rig.Note.Screen == FakeElementNote.FirstStart, "a new profile starts with a new install's key viewers");
    Check(rig.Files.Exists(first) && !rig.Files.Exists(second), "only the profile left has a file");

    rig.Note.Screen = "stream edits";
    rig.Service.Switch(first);
    rig.Tick();
    Check(rig.Note.Screen == "mine" && rig.Note.Owner == first, "switching back brings the first profile's");
    Check(!rig.Files.Exists(first), "the file on screen is gone, so it can't go stale");
    Check(rig.Files.Read(rig.Files.PathOf(second)) == "stream edits", "the edits stay with their profile");
    Check(rig.Note.HistoryCleared, "ElementNote loads them for a profile, not as an undoable load");
  }

  private static void DuplicatesAndImportsBringKeyViewers()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    ElementProfile shown = rig.Service.Active;
    rig.Service.Duplicate(shown.Id, "Copy");
    rig.Tick();
    ElementProfile copy = rig.Library.Profiles[1];
    Check(rig.FileOf(copy) == "mine", "a copy of the profile on screen gets the key viewers on screen");

    rig.Service.Create("Fresh");
    rig.Tick();
    ElementProfile fresh = rig.Service.Active;
    rig.Service.Switch(shown.Id);
    rig.Tick();
    rig.Service.Duplicate(fresh.Id, "Fresh copy");
    ElementProfile freshCopy = rig.Library.Profiles.First(profile => profile.Name == "Fresh copy");
    Check(rig.FileOf(freshCopy) == FakeElementNote.FirstStart, "a copy of a saved profile copies its file");

    rig.Service.Create("Never shown");
    ElementProfile never = rig.Service.Active;
    rig.Service.Duplicate(never.Id, "Never copy");
    ElementProfile neverCopy = rig.Library.Profiles.First(profile => profile.Name == "Never copy");
    Check(
      neverCopy.KeyViewersStart == KeyViewersStart.FirstStart,
      "a copy of a profile never shown starts the same way"
    );

    rig.Dialogs.Next = FileDialogResult.Chosen(rig.WriteProfileFile("Shared", "shared viewers"));
    ProfileOutcome imported = rig.Service.Import("Import");
    rig.Tick();
    Check(imported.Ok && rig.Note.Screen == "shared viewers", "an imported profile shows the key viewers it came with");
  }

  private static void ExportsCarryTheKeyViewers()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    ElementProfile shown = rig.Service.Active;
    ProfileFile file = rig.Export(shown, out ProfileOutcome outcome);
    Check(outcome.Ok && !outcome.KeyViewersLeftOut, "the export is written");
    Check(file.KeyViewers == "mine", "the profile on screen exports what ElementNote saves now");
    Check(!File.Exists(rig.Files.ExportPathOf(shown.Id)), "ElementNote's export file is cleaned up");

    rig.Service.Create("Other");
    rig.Tick();
    Check(rig.Export(shown, out _).KeyViewers == "mine", "a profile not on screen exports its file");

    // A waiting profile's file keeps its press counts on this PC.
    File.WriteAllText(rig.Files.PathOf(shown.Id), "{\"keys\": {}, \"keyCounters\": {\"hand\": {\"A\": 9}}}");
    string shared = rig.Export(shown, out _).KeyViewers;
    Check(
      shared.Contains("\"keys\"") && !shared.Contains("keyCounters"),
      "an export leaves press counts out: " + shared
    );

    rig.Note.Connected = false;
    ProfileFile without = rig.Export(rig.Service.Active, out ProfileOutcome missed);
    Check(
      missed.Ok && missed.KeyViewersLeftOut && without.KeyViewers == null,
      "without ElementNote the file says it left them out"
    );
  }

  private static void DeletedKeyViewersGoToBackups()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    ElementProfile first = rig.Service.Active;
    rig.Service.Create("Second");
    rig.Tick();
    rig.Service.Delete(first.Id);
    Check(!rig.Files.Exists(first.Id), "a deleted profile's file leaves the profiles");
    Check(rig.Backups().Any(text => text == "mine"), "and waits in Backups");

    rig.Note.Screen = "second";
    rig.Service.Create("Third");
    rig.Tick();
    ElementProfile second = rig.Library.Profiles.First(profile => profile.Name == "Second");
    rig.Service.Switch(second.Id);
    rig.Tick();
    rig.Service.Delete(second.Id);
    rig.Tick();
    Check(rig.Backups().Any(text => text == "second"), "a deleted profile on screen goes to Backups as it leaves");
  }

  private static void AStaleOwnerIsNeverOverwritten()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    string first = rig.Service.Active.Id;
    rig.Service.Create("Second");
    rig.Tick();
    string second = rig.Service.Active.Id;

    // The game gives up on a swap that ElementNote then finishes: whose key viewers are on
    // screen is unknown, and nothing may be saved or removed on a guess.
    rig.Note.Hold = true;
    rig.Service.Switch(first);
    rig.Tick();
    rig.Note.Release(answered: false);
    rig.Note.Hold = false;
    rig.Tick();
    Check(rig.Note.Owner == first && rig.Note.Screen == "mine", "the next swap learns the real owner first");
    Check(
      rig.FileOf(rig.Library.Profiles.First(profile => profile.Id == second)) == FakeElementNote.FirstStart,
      "the profile left keeps its own"
    );
    Check(
      rig.Backups().Length == 0 && rig.KeyViewers.FailedProfileName == null,
      "nothing went to Backups, and nothing failed"
    );

    rig.Service.Switch(second);
    rig.Tick();
    Check(
      rig.Note.Screen == FakeElementNote.FirstStart && rig.FileOf(rig.Library.Profiles[0]) == "mine",
      "switching goes on as before"
    );

    // A game that quit before a swap answered leaves a stale file for the profile on screen.
    File.WriteAllText(rig.Files.PathOf(second), "stale");
    rig.Note.Reconnect();
    rig.Tick();
    Check(
      !rig.Files.Exists(second) && rig.Note.Screen == FakeElementNote.FirstStart,
      "a stale file of the profile on screen is removed"
    );
  }

  private static void WithoutElementNoteNothingIsLost()
  {
    using Rig rig = new();
    rig.Note.Screen = "mine";
    rig.Tick();
    string first = rig.Service.Active.Id;
    rig.Note.Connected = false;
    rig.Service.Create("Offline");
    rig.Service.Duplicate(first, "Copy");
    rig.Tick();
    Check(rig.Note.TrueOwner == first && rig.Note.Screen == "mine", "nothing changes while ElementNote is away");

    rig.Note.Reconnect();
    rig.Tick();
    Check(rig.Note.Screen == FakeElementNote.FirstStart, "ElementNote catches up when it connects");
    ElementProfile copy = rig.Library.Profiles.First(profile => profile.Name == "Copy");
    rig.Service.Switch(copy.Id);
    rig.Tick();
    Check(rig.Note.Screen == "mine", "a copy made offline starts with its source's key viewers");
  }

  // ---- The rig ----

  private sealed class Rig : IDisposable
  {
    private readonly TempFolder _folder = new("element-key-viewer-tests-");
    public readonly ElementSettings Settings = Fresh();
    public readonly FakeElementNote Note = new();
    public readonly FakeDialogs Dialogs = new();
    public readonly ProfileLibrary Library;
    public readonly ProfileKeyViewerFiles Files;
    public readonly ProfileKeyViewers KeyViewers;
    public readonly ProfileService Service;

    public Rig()
    {
      Library = new ProfileLibrary(Settings, "Default");
      Files = new ProfileKeyViewerFiles(_folder.Path);
      KeyViewers = new ProfileKeyViewers(
        () => Settings,
        Note,
        Files,
        (context, exception) => throw new InvalidOperationException(context, exception),
        _ => { }
      );
      Note.Files = Files;
      Service = new ProfileService(
        Library,
        Dialogs,
        new ProfileFileStore(),
        () => true,
        () => { },
        () => { },
        "1.0.0",
        (context, exception) => throw new InvalidOperationException(context, exception),
        KeyViewers
      );
    }

    public void Tick()
    {
      for (int frame = 0; frame < 4; frame++)
        KeyViewers.Tick();
    }

    public ElementProfile AddOldProfile(string name)
    {
      ElementProfile profile = new() { Name = name, Content = ProfileContent.Capture(Settings) };
      Settings.Profiles.Add(profile);
      return profile;
    }

    public string FileOf(ElementProfile profile) =>
      Files.Exists(profile.Id) ? Files.Read(Files.PathOf(profile.Id)) : null;

    public string[] Backups()
    {
      string folder = Path.Combine(_folder.Path, SettingsBackup.FolderName);
      return Directory.Exists(folder)
        ? Directory.GetFiles(folder, "KeyViewers-*.json").Select(File.ReadAllText).ToArray()
        : Array.Empty<string>();
    }

    public ProfileFile Export(ElementProfile profile, out ProfileOutcome outcome)
    {
      string path = Path.Combine(_folder.Path, "export-" + Guid.NewGuid().ToString("N"));
      Dialogs.Next = FileDialogResult.Chosen(path);
      outcome = Service.Export(profile.Id, "Export").Result;
      return new ProfileFileStore().Read(path + "." + ProfileFile.Extension).File;
    }

    public string WriteProfileFile(string name, string keyViewers)
    {
      string path = Path.Combine(_folder.Path, name + "." + ProfileFile.Extension);
      ProfileLibrary source = new(Fresh(), name);
      ProfileFile file = source.Export(source.Active.Id, "1.0.0");
      file.KeyViewers = keyViewers;
      Check(new ProfileFileStore().Write(path, file) == null, "the profile file to import is written");
      return path;
    }

    public void Dispose() => _folder.Dispose();
  }

  // ElementNote in one object: what is on screen and whose it really is, what the game last
  // heard, and the files it reads and writes.
  private sealed class FakeElementNote : IElementNoteKeyViewers
  {
    public const string FirstStart = "first start";
    public ProfileKeyViewerFiles Files;
    public string Screen = "";
    public string TrueOwner;
    public int Swaps;
    public bool HistoryCleared;
    public bool Hold;
    private bool _connected = true;
    private string _heard;
    private TaskCompletionSource<bool> _held;
    private Action _heldWork;

    public event Action Changed;

    public bool Connected
    {
      get => _connected;
      set
      {
        _connected = value;
        if (value)
          _heard = TrueOwner;
        Changed?.Invoke();
      }
    }

    public string Owner => _connected ? _heard : null;

    public void Reconnect() => Connected = true;

    // Answered: the game hears whose key viewers are on screen. Not answered: it gave up and
    // doesn't know.
    public void Release(bool answered)
    {
      _heldWork();
      _heard = answered ? TrueOwner : null;
      _held.TrySetResult(answered);
      if (answered)
        Changed?.Invoke();
    }

    public Task<bool> SaveAsync(string path)
    {
      if (!_connected)
        return Task.FromResult(false);
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      File.WriteAllText(path, Screen);
      return Task.FromResult(true);
    }

    public Task<bool> SwapAsync(string savePath, KeyViewersSource load, string owner, string expectedOwner)
    {
      if (!_connected)
        return Task.FromResult(false);
      if (TrueOwner != expectedOwner)
      {
        // Refused, and the answer says whose key viewers are on screen.
        _heard = TrueOwner;
        Changed?.Invoke();
        return Task.FromResult(false);
      }
      void Work()
      {
        if (savePath != null)
        {
          Directory.CreateDirectory(Path.GetDirectoryName(savePath));
          File.WriteAllText(savePath, Screen);
        }
        Screen = load.Kind switch
        {
          KeyViewersSourceKind.File => File.ReadAllText(load.Path),
          KeyViewersSourceKind.FirstStart => FirstStart,
          _ => Screen,
        };
        HistoryCleared = load.Kind != KeyViewersSourceKind.Keep || HistoryCleared;
        TrueOwner = owner;
        Swaps++;
      }
      if (Hold)
      {
        _held = new TaskCompletionSource<bool>();
        _heldWork = Work;
        return _held.Task;
      }
      Work();
      _heard = TrueOwner;
      Changed?.Invoke();
      return Task.FromResult(true);
    }
  }

  private sealed class FakeDialogs : IFileDialogs
  {
    public FileDialogResult Next = FileDialogResult.Cancelled;

    public FileDialogResult Open(FileDialogRequest request) => Next;

    public FileDialogResult Save(FileDialogRequest request) => Next;
  }
}
