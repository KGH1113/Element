using System;
using System.ComponentModel;
using System.Threading;
using UnityEngine;

namespace Element.Infrastructure.DmNoteApp;

/// <summary>
/// Auto open/close ElementNote: opens the bundled app with the game unless it
/// is already running. The app closes itself once the game is gone.
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
  private int _busy;

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

  /// <summary>Returns at once; installing and starting run on a worker thread.</summary>
  public void OpenIfClosed()
  {
    if (_app == null || !_app.IsBundled)
      return;
    if (Interlocked.Exchange(ref _busy, 1) == 1)
      return;
    _runInBackground(Open);
  }

  private void Open()
  {
    try
    {
      if (_app.IsRunning())
        return;
      _app.Install();
      _app.Launch();
      _log("Opened ElementNote with the game.");
    }
    catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
    {
      _logWarning("ElementNote stayed closed because its administrator prompt was declined.");
    }
    catch (Exception exception)
    {
      _logError("Open ElementNote", exception);
    }
    finally
    {
      Interlocked.Exchange(ref _busy, 0);
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
