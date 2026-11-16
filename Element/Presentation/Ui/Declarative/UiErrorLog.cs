using System;
using System.Collections.Generic;

namespace Element.Presentation.Ui.Declarative;

/// <summary>
/// Writes what ElementUI caught (a view, view model or effect that threw) to
/// the mod's log. ElementUI keeps the screen running after such an error, so
/// without this the game's log never shows it. Each different error is
/// written once, and only the first few, so one that repeats every frame
/// can't flood the log.
/// </summary>
internal sealed class UiErrorLog : IDisposable
{
  private const int Limit = 32;
  private readonly Action<string, Exception> _log;
  private readonly HashSet<string> _written = new();

  public UiErrorLog(Action<string, Exception> log)
  {
    _log = log;
    ElementUI.Runtime.ErrorReported += Write;
  }

  private void Write(Exception exception)
  {
    if (exception == null || _written.Count >= Limit)
      return;
    string trace = exception.StackTrace ?? "";
    int line = trace.IndexOf('\n');
    string key = exception.GetType().FullName + ": " + exception.Message + " " + (line < 0 ? trace : trace.Substring(0, line));
    if (_written.Add(key))
      _log("ElementUI", exception);
  }

  public void Dispose() => ElementUI.Runtime.ErrorReported -= Write;
}
