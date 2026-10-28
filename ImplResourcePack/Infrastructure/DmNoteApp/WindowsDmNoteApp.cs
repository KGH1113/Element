using System;
using System.Diagnostics;
using System.IO;

namespace ImplResourcePack.Infrastructure.DmNoteApp;

/// <summary>
/// The mod ships the portable app in <c>ImplDmNote/windows</c> and installs it
/// to <c>%LOCALAPPDATA%\ImplResourcePack\ImplDmNote</c>.
/// </summary>
internal sealed class WindowsDmNoteApp : BundledDmNoteApp
{
  private const string ProcessName = "impl-dm-note";
  private const string ExecutableName = ProcessName + ".exe";

  public WindowsDmNoteApp(string modPath)
    : base(
      Path.Combine(modPath, "ImplDmNote", "windows", ExecutableName),
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImplResourcePack"),
      "ImplDmNote"
    ) { }

  public override bool IsRunning()
  {
    Process[] running = Process.GetProcessesByName(ProcessName);
    foreach (Process process in running)
      process.Dispose();
    return running.Length > 0;
  }

  // The release asks for administrator rights, which only ShellExecute can
  // request; Windows shows its UAC prompt.
  public override void Launch()
  {
    using Process process = Process.Start(
      new ProcessStartInfo(Path.Combine(InstalledPath, ExecutableName), LaunchedByGameArgument)
      {
        UseShellExecute = true,
        WorkingDirectory = InstalledPath,
      }
    );
  }

  protected override void Unpack(string source, string staging) =>
    CopyDirectory(Path.GetDirectoryName(source), Path.Combine(staging, "ImplDmNote"));

  private static void CopyDirectory(string source, string destination)
  {
    Directory.CreateDirectory(destination);
    foreach (string file in Directory.GetFiles(source))
      File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    foreach (string directory in Directory.GetDirectories(source))
      CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
  }
}
