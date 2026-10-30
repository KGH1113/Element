using System;
using System.IO;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>The ElementNote copy this mod ships for the current operating system.</summary>
internal interface IDmNoteApp
{
  /// <summary>False when this mod build carries no ElementNote for this OS.</summary>
  bool IsBundled { get; }

  bool IsRunning();

  /// <summary>Copies the bundled app out of the mod folder when it changed.</summary>
  void Install();

  /// <summary>Opens the installed app in the background, owned by the game.</summary>
  void Launch();
}

/// <summary>
/// The app never runs from the mod folder: a running executable there would
/// block mod updates on Windows, and an unzipped bundle may lose the macOS
/// executable bit. It is installed once per mod build to a fixed folder, so
/// granted OS permissions and saved settings keep applying.
/// </summary>
internal abstract class BundledDmNoteApp : IDmNoteApp
{
  /// <summary>Tells ElementNote to start in the background and close with the game.</summary>
  public const string LaunchedByGameArgument = "--launched-by-game";

  private readonly string _source;
  private readonly string _installRoot;
  private readonly string _installName;

  protected BundledDmNoteApp(string source, string installRoot, string installName)
  {
    _source = source;
    _installRoot = installRoot;
    _installName = installName;
  }

  public bool IsBundled => File.Exists(_source);

  protected string InstalledPath => Path.Combine(_installRoot, _installName);

  private string StampPath => InstalledPath + ".stamp";

  public abstract bool IsRunning();

  public abstract void Launch();

  public void Install()
  {
    string stamp = Stamp(_source);
    if (Exists(InstalledPath) && File.Exists(StampPath) && File.ReadAllText(StampPath).Trim() == stamp)
      return;
    Directory.CreateDirectory(_installRoot);
    string staging = InstalledPath + ".installing";
    DeleteDirectory(staging);
    Directory.CreateDirectory(staging);
    try
    {
      Unpack(_source, staging);
      string unpacked = Path.Combine(staging, _installName);
      if (!Exists(unpacked))
        throw new IOException("The bundled ElementNote is incomplete. Reinstall the mod.");
      // The stamp goes first so an interrupted swap installs again next time.
      if (File.Exists(StampPath))
        File.Delete(StampPath);
      DeleteDirectory(InstalledPath);
      Directory.Move(unpacked, InstalledPath);
      File.WriteAllText(StampPath, stamp);
    }
    finally
    {
      DeleteDirectory(staging);
    }
  }

  /// <summary>Writes the app into <paramref name="staging"/> under its install name.</summary>
  protected abstract void Unpack(string source, string staging);

  // A new mod build replaces the bundled file, which changes its size or time.
  private static string Stamp(string file)
  {
    var info = new FileInfo(file);
    return info.Length + ":" + info.LastWriteTimeUtc.Ticks;
  }

  private static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

  private static void DeleteDirectory(string path)
  {
    if (Directory.Exists(path))
      Directory.Delete(path, true);
  }
}
