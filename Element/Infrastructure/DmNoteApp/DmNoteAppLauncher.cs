using System;
using System.ComponentModel;
using System.Threading;
using UnityEngine;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>
/// Keeps the bundled ElementNote installed where players can open it, and
/// auto opens it with the game unless it is already running. The app closes
/// itself once the game is gone.
/// </summary>
internal sealed class DmNoteAppLauncher
{
  // ShellExecute reports a declined UAC prompt as ERROR_CANCELLED.
  private const int ErrorCancelled = 1223;

  private readonly IDmNoteApp _app;
  private readonly Action<Action> _runInBackground;
  private readonly Action<string> _log;
  private readonly Action<string> _logWarning;
  private readonly Action<string, Exception> _logError;

  // One request at a time: a later one finds the app installed or running.
  private readonly object _gate = new();

  public DmNoteAppLauncher(
    IDmNoteApp app,
    Action<Action> runInBackground,
    Action<string> log,
    Action<string> logWarning,
    Action<string, Exception> logError
  )
  {
    _app = app;
    _runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
    _log = log ?? (_ => { });
    _logWarning = logWarning ?? (_ => { });
    _logError = logError ?? ((_, _) => { });
  }

  public static DmNoteAppLauncher Create(
    string modPath,
    Action<string> log,
    Action<string> logWarning,
    Action<string, Exception> logError
  ) => new(ForCurrentPlatform(modPath), RunOnThread, log, logWarning, logError);

  /// <summary>Installs or updates the app unless it is running. Returns at once; the work runs on a worker thread.</summary>
  public void InstallIfClosed() => Start(open: false);

  /// <summary>Installs the app like <see cref="InstallIfClosed"/>, then opens it.</summary>
  public void OpenIfClosed() => Start(open: true);

  private void Start(bool open)
  {
    if (_app == null || !_app.IsBundled)
      return;
    _runInBackground(() => Prepare(open));
  }

  private void Prepare(bool open)
  {
    lock (_gate)
    {
      try
      {
        // A running copy cannot be replaced; the next game start updates it.
        if (_app.IsRunning())
          return;
        _app.Install();
        EnsureListed();
        if (!open)
          return;
        _app.Launch();
        _log("Opened ElementNote with the game.");
      }
      catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
      {
        _logWarning("ElementNote stayed closed because its administrator prompt was declined.");
      }
      catch (Exception exception)
      {
        _logError(open ? "Open ElementNote" : "Install ElementNote", exception);
      }
    }
  }

  // Opening it with the game still works without the shortcut.
  private void EnsureListed()
  {
    try
    {
      _app.EnsureListed();
    }
    catch (Exception exception)
    {
      _logWarning("ElementNote was installed but could not be added to the app list: " + exception.Message);
    }
  }

  private static IDmNoteApp ForCurrentPlatform(string modPath) =>
    UnityEngine.Application.platform switch
    {
      RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => new MacDmNoteApp(modPath),
      RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => new WindowsDmNoteApp(modPath),
      _ => null,
    };

  private static void RunOnThread(Action work) =>
    new Thread(() => work()) { IsBackground = true, Name = "ElementNote launcher" }.Start();
}
