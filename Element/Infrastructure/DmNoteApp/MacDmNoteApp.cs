using System;
using System.IO;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>
/// The mod ships <c>ElementNote/macos/ElementNote.app.zip</c>, made by <c>ditto</c>,
/// and installs it to <c>~/Library/Application Support/io.github.kgh1113.element</c>.
/// </summary>
internal sealed class MacDmNoteApp : BundledDmNoteApp
{
  private const string AppName = "ElementNote.app";
  // CFBundleExecutable; the keyboard helper it starts shares the name.
  private const string ExecutableName = "element-note";
  private static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(10);
  private static readonly TimeSpan UnpackTimeout = TimeSpan.FromSeconds(60);

  public MacDmNoteApp(string modPath)
    : base(
      Path.Combine(modPath, "ElementNote", "macos", AppName + ".zip"),
      Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", "Library", "Application Support", "io.github.kgh1113.element"),
      AppName
    ) { }

  public override bool IsRunning() => SystemCommand.Run("/usr/bin/pgrep", "-x " + ExecutableName, QuickTimeout) == 0;

  // -g keeps the game in front. LaunchServices reuses a running instance.
  public override void Launch() =>
    SystemCommand.RunOrThrow(
      "/usr/bin/open",
      "-g -a " + SystemCommand.Quote(InstalledPath) + " --args " + LaunchedByGameArgument,
      QuickTimeout
    );

  // ditto keeps the executable bit and code signature a plain unzip drops.
  // The archive is part of this mod, which already runs native code in the
  // game, so its copy does not inherit the downloaded zip's quarantine.
  protected override void Unpack(string source, string staging) =>
    SystemCommand.RunOrThrow(
      "/usr/bin/ditto",
      "-x -k --noqtn " + SystemCommand.Quote(source) + " " + SystemCommand.Quote(staging),
      UnpackTimeout
    );
}
