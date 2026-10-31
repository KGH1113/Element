using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Element.Infrastructure.DmNoteApp;

namespace Element.Tests;

internal static class DmNoteAppTests
{
  public static void Run()
  {
    InstallsOncePerBundledBuild();
    ReplacesAnOutdatedInstall();
    OpensOnlyWhenClosed();
    SkipsBuildsWithoutTheApp();
    ReportsADeclinedAdministratorPrompt();
  }

  private static void InstallsOncePerBundledBuild()
  {
    using var folders = new Folders();
    var app = new FolderApp(folders);
    app.Install();
    app.Install();
    Check(app.Unpacks == 1, "An unchanged bundle must not be copied again.");
    Check(File.ReadAllText(Path.Combine(folders.Installed, "app.bin")) == "v1", "The bundled app is installed.");
    Check(!Directory.Exists(folders.Installed + ".installing"), "The staging folder is removed.");
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

  private static void OpensOnlyWhenClosed()
  {
    var app = new FakeApp { Running = true };
    Launcher(app, out _).OpenIfClosed();
    Check(app.Launches == 0 && app.Installs == 0, "A running ElementNote is left alone.");
    app.Running = false;
    Launcher(app, out _).OpenIfClosed();
    Check(app.Installs == 1 && app.Launches == 1, "A closed ElementNote is installed and opened.");
  }

  private static void SkipsBuildsWithoutTheApp()
  {
    var app = new FakeApp { Bundled = false };
    Launcher(app, out _).OpenIfClosed();
    Check(app.Launches == 0, "A mod build without the app does nothing.");
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
    public int Launches;
    public Exception LaunchError;

    public bool IsBundled => Bundled;

    public bool IsRunning() => Running;

    public void Install() => Installs++;

    public void Launch()
    {
      Launches++;
      if (LaunchError != null)
        throw LaunchError;
    }
  }

  private sealed class FolderApp : BundledDmNoteApp
  {
    private readonly Folders _folders;

    public FolderApp(Folders folders)
      : base(folders.Source, folders.Root, "App")
    {
      _folders = folders;
    }

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
    public string Installed => Path.Combine(Root, "App");

    public void Dispose() => Directory.Delete(Root, true);
  }

  private static void Check(bool value, string message)
  {
    if (!value)
      throw new InvalidOperationException(message);
  }
}
