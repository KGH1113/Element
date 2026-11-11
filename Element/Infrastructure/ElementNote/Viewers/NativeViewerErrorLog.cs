using System;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// The native viewers' log. A failure that repeats on every poll or check is
/// written once, until a different one comes.
/// </summary>
internal sealed class NativeViewerErrorLog
{
  private readonly Action<string, Exception> _logError;
  private readonly Action<string> _logWarning;
  private string _lastLogged;

  public NativeViewerErrorLog(Action<string, Exception> logError, Action<string> logWarning)
  {
    _logError = logError ?? ((_, _) => { });
    _logWarning = logWarning ?? (_ => { });
  }

  public void Error(string context, Exception error)
  {
    string key = context + ": " + error.GetType().Name + ": " + error.Message;
    if (key == _lastLogged)
      return;
    _lastLogged = key;
    _logError(context, error);
  }

  /// <summary>A page or host problem the native host reported.</summary>
  public void PageFault(string message)
  {
    if (message == _lastLogged)
      return;
    _lastLogged = message;
    _logWarning("[Native viewers] " + message);
  }

  /// <summary>Written every time.</summary>
  public void Warning(string message) => _logWarning(message);
}
