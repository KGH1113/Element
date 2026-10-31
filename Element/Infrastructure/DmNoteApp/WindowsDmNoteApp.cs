using System;
using System.Diagnostics;
using System.IO;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>
/// The mod ships the portable app in <c>ElementNote/windows</c>, installs it
/// to <c>%LOCALAPPDATA%\io.github.kgh1113.element\ElementNote</c> and lists it
/// in the Start Menu.
/// </summary>
internal sealed class WindowsDmNoteApp : BundledDmNoteApp
{
  private const string AppName = "ElementNote";
  private const string ProcessName = "element-note";
  private const string ExecutableName = ProcessName + ".exe";

  public WindowsDmNoteApp(string modPath)
    : this(
      modPath,
      Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "io.github.kgh1113.element"
      )
    ) { }

  // The folder stays where 1.0.0 put it: Windows Firewall remembers the app by its path.
  private WindowsDmNoteApp(string modPath, string dataFolder)
    : base(
      Path.Combine(modPath, "ElementNote", "windows", ExecutableName),
      Path.Combine(dataFolder, AppName),
      dataFolder
    ) { }

  private string Executable => Path.Combine(InstalledPath, ExecutableName);

  public override bool IsRunning()
  {
    Process[] running = Process.GetProcessesByName(ProcessName);
    foreach (Process process in running)
      process.Dispose();
    return running.Length > 0;
  }

  public override void EnsureListed()
  {
    string shortcut = StartMenuShortcut.PathFor(AppName);
    if (!File.Exists(shortcut))
      StartMenuShortcut.Create(shortcut, Executable, InstalledPath, AppName);
  }

  // The release asks for administrator rights, which only ShellExecute can
  // request; Windows shows its UAC prompt.
  public override void Launch()
  {
    using Process process = Process.Start(
      new ProcessStartInfo(Executable, LaunchedByGameArgument)
      {
        UseShellExecute = true,
        WorkingDirectory = InstalledPath,
      }
    );
  }

  protected override void Unpack(string source, string staging) =>
    CopyDirectory(Path.GetDirectoryName(source), Path.Combine(staging, AppName));

  private static void CopyDirectory(string source, string destination)
  {
    Directory.CreateDirectory(destination);
    foreach (string file in Directory.GetFiles(source))
      File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    foreach (string directory in Directory.GetDirectories(source))
      CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
  }
}
