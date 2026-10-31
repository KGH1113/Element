using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using Element.Infrastructure.DmNoteApp;

namespace Element.Tests;

internal static class DmNoteAppTests
{
  public static void Run()
  {
    InstallsOncePerBundledBuild();
    ReplacesAnOutdatedInstall();
    RemovesTheFormerInstall();
    OpensOnlyWhenClosed();
    InstallsWithoutOpening();
    OpensWhenTheAppListFails();
    SkipsBuildsWithoutTheApp();
    ReportsADeclinedAdministratorPrompt();
    QuotesShortcutPathsForPowerShell();
  }

  private static void InstallsOncePerBundledBuild()
  {
    using var folders = new Folders();
    var app = new FolderApp(folders);
    app.Install();
    app.Install();
    Check(app.Unpacks == 1, "An unchanged bundle must not be copied again.");
    Check(File.ReadAllText(Path.Combine(folders.Installed, "app.bin")) == "v1", "The bundled app is installed.");
    Check(
      Directory.GetFileSystemEntries(folders.Apps).Length == 1,
      "The apps folder players see holds only the app, not its stamp or staging copy."
    );
    Check(File.Exists(Path.Combine(folders.State, "App.stamp")), "The stamp is kept in the state folder.");
    Check(!Directory.Exists(Path.Combine(folders.State, "App.installing")), "The staging folder is removed.");
  }

  private static void ReplacesAnOutdatedInstall()
  {
    using var folders = new Folders();
    var app = new FolderApp(folders);
    app.Install();
    File.WriteAllText(folders.Source, "v2-longer");
    app.Install();
    Check(app.Unpacks == 2, "A new mod build installs its own app.");
    Check(
      File.ReadAllText(Path.Combine(folders.Installed, "app.bin")) == "v2-longer",
      "The new build replaces the old one."
    );
  }

  private static void RemovesTheFormerInstall()
  {
    using var folders = new Folders();
    var app = new FolderApp(folders);
    app.Install();
    // How 1.0.0 left it: this build's stamp, with the app next to it.
    Directory.Move(folders.Installed, folders.Former);
    app.Install();
    Check(
      app.Unpacks == 2 && Directory.Exists(folders.Installed),
      "A stamp without the app at its new place installs it again."
    );
    Check(!Directory.Exists(folders.Former), "The copy at the former place is removed, so only one is listed.");
    Directory.CreateDirectory(folders.Former);
    app.Install();
    Check(
      app.Unpacks == 2 && !Directory.Exists(folders.Former),
      "A leftover former copy goes on a later start without copying the app again."
    );
  }

  private static void OpensOnlyWhenClosed()
  {
    var app = new FakeApp { Running = true };
    Launcher(app, out _).OpenIfClosed();
    Check(app.Launches == 0 && app.Installs == 0, "A running ElementNote is left alone.");
    app.Running = false;
    Launcher(app, out _).OpenIfClosed();
    Check(
      app.Installs == 1 && app.Listings == 1 && app.Launches == 1,
      "A closed ElementNote is installed, listed and opened."
    );
  }

  private static void InstallsWithoutOpening()
  {
    var app = new FakeApp();
    Launcher(app, out _).InstallIfClosed();
    Check(app.Installs == 1 && app.Listings == 1, "Auto open off still installs and lists the app.");
    Check(app.Launches == 0, "Installing alone does not open the app.");
  }

  private static void OpensWhenTheAppListFails()
  {
    var app = new FakeApp { ListError = new IOException("no Start Menu") };
    Launcher(app, out List<string> warnings).OpenIfClosed();
    Check(warnings.Count == 1, "A missing shortcut is a warning.");
    Check(app.Launches == 1, "The game still opens the app without its shortcut.");
  }

  private static void SkipsBuildsWithoutTheApp()
  {
    var app = new FakeApp { Bundled = false };
    Launcher(app, out _).OpenIfClosed();
    Launcher(app, out _).InstallIfClosed();
    Check(app.Installs == 0 && app.Launches == 0, "A mod build without the app does nothing.");
  }

  private static void ReportsADeclinedAdministratorPrompt()
  {
    var app = new FakeApp { LaunchError = new Win32Exception(1223) };
    DmNoteAppLauncher launcher = Launcher(app, out List<string> warnings);
    launcher.OpenIfClosed();
    Check(warnings.Count == 1, "A declined UAC prompt is a warning, not an error.");
    app.LaunchError = null;
    launcher.OpenIfClosed();
    Check(app.Launches == 2, "A later request tries again.");
  }

  private static void QuotesShortcutPathsForPowerShell()
  {
    Check(
      StartMenuShortcut.Literal(@"C:\Users\홍길동") == @"'C:\Users\홍길동'",
      "Non-ASCII paths pass through unchanged."
    );
    Check(
      StartMenuShortcut.Literal(@"C:\Users\O'Neil") == @"'C:\Users\O''Neil'",
      "A quote in a user folder is doubled."
    );
    Check(StartMenuShortcut.Literal("D’Arcy") == "'D’’Arcy'", "PowerShell's curly quotes are doubled too.");
    string script = StartMenuShortcut.Script(@"C:\a.lnk", @"C:\b.exe", @"C:\", "ElementNote");
    Check(
      script.Contains(@"[ElementNoteShortcut]::Save('C:\a.lnk', 'C:\b.exe', 'C:\', 'ElementNote')"),
      "The script saves the given paths."
    );
    Check(
      Encoding.Unicode.GetString(Convert.FromBase64String(StartMenuShortcut.Encode(script))) == script,
      "The encoded command decodes to the script."
    );
  }

  private static DmNoteAppLauncher Launcher(IDmNoteApp app, out List<string> warnings)
  {
    var logged = new List<string>();
    warnings = logged;
    return new DmNoteAppLauncher(
      app,
      work => work(),
      _ => { },
      logged.Add,
      (context, error) => throw new InvalidOperationException(context, error)
    );
  }

  private sealed class FakeApp : IDmNoteApp
  {
    public bool Bundled = true;
    public bool Running;
    public int Installs;
    public int Listings;
    public int Launches;
    public Exception ListError;
    public Exception LaunchError;

    public bool IsBundled => Bundled;

    public bool IsRunning() => Running;

    public void Install() => Installs++;

    public void EnsureListed()
    {
      Listings++;
      if (ListError != null)
        throw ListError;
    }

    public void Launch()
    {
      Launches++;
      if (LaunchError != null)
        throw LaunchError;
    }
  }

  private sealed class FolderApp : BundledDmNoteApp
  {
    public FolderApp(Folders folders)
      : base(folders.Source, folders.Installed, folders.State, folders.Former) { }

    public int Unpacks { get; private set; }

    public override bool IsRunning() => false;

    public override void Launch() { }

    protected override void Unpack(string source, string staging)
    {
      Unpacks++;
      string app = Path.Combine(staging, "App");
      Directory.CreateDirectory(app);
      File.Copy(source, Path.Combine(app, "app.bin"));
    }
  }

  // Mirrors macOS: the app goes to an apps folder, its stamp to a state
  // folder that also held the former copy.
  private sealed class Folders : IDisposable
  {
    public Folders()
    {
      Root = Path.Combine(Path.GetTempPath(), "element-note-app-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Root);
      Source = Path.Combine(Root, "bundled.bin");
      File.WriteAllText(Source, "v1");
    }

    public string Root { get; }
    public string Source { get; }
    public string Apps => Path.Combine(Root, "Applications");
    public string Installed => Path.Combine(Apps, "App");
    public string State => Path.Combine(Root, "State");
    public string Former => Path.Combine(State, "App");

    public void Dispose() => Directory.Delete(Root, true);
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
