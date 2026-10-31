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

  /// <summary>Makes the installed app show up where the OS lists apps, so it opens without the game.</summary>
  void EnsureListed();

  /// <summary>Opens the installed app in the background, owned by the game.</summary>
  void Launch();
}

/// <summary>
/// The app never runs from the mod folder: a running executable there would
/// block mod updates on Windows, and an unzipped bundle may lose the macOS
/// executable bit. It is installed once per mod build to a fixed place where
/// players can open it themselves, and granted OS permissions and saved
/// settings keep applying.
/// </summary>
internal abstract class BundledDmNoteApp : IDmNoteApp
{
  /// <summary>Tells ElementNote to start in the background and close with the game.</summary>
  public const string LaunchedByGameArgument = "--launched-by-game";

  private readonly string _source;
  private readonly string _stateFolder;
  private readonly string _formerInstalledPath;

  /// <param name="source">The app file this mod build carries.</param>
  /// <param name="installedPath">Where players find the app.</param>
  /// <param name="stateFolder">Holds the install stamp and staging copy, out of the players' sight.</param>
  /// <param name="formerInstalledPath">An earlier install location, removed once the app moved.</param>
  protected BundledDmNoteApp(string source, string installedPath, string stateFolder, string formerInstalledPath = null)
  {
    _source = source;
    InstalledPath = installedPath;
    _stateFolder = stateFolder;
    _formerInstalledPath = formerInstalledPath;
  }

  public bool IsBundled => File.Exists(_source);

  protected string InstalledPath { get; }

  private string InstallName => Path.GetFileName(InstalledPath);

  private string StampPath => Path.Combine(_stateFolder, InstallName + ".stamp");

  public abstract bool IsRunning();

  public virtual void EnsureListed() { }

  public abstract void Launch();

  public void Install()
  {
    string stamp = Stamp(_source);
    if (!Exists(InstalledPath) || !File.Exists(StampPath) || File.ReadAllText(StampPath).Trim() != stamp)
      Replace(stamp);
    RemoveFormerCopy();
  }

  private void Replace(string stamp)
  {
    Directory.CreateDirectory(_stateFolder);
    Directory.CreateDirectory(Path.GetDirectoryName(InstalledPath));
    string staging = Path.Combine(_stateFolder, InstallName + ".installing");
    DeleteDirectory(staging);
    Directory.CreateDirectory(staging);
    try
    {
      Unpack(_source, staging);
      string unpacked = Path.Combine(staging, InstallName);
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

  // Only one copy stays, so the OS app list does not show two. A copy that
  // cannot be removed yet does not stop the app from opening; the next game
  // start tries again.
  private void RemoveFormerCopy()
  {
    try
    {
      if (_formerInstalledPath != null)
        DeleteDirectory(_formerInstalledPath);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
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
