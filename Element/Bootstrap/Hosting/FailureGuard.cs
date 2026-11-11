using System;
using System.Collections.Generic;

namespace Element.Bootstrap.Hosting;

/// <summary>
/// Runs the mod's work for the game so a failure stays in the mod: game callbacks,
/// the frame loop and settings applying. Per-frame work can fail every frame, so the
/// first failure of each kind is logged with its stack trace, then only a short count.
/// </summary>
internal sealed class FailureGuard
{
  private readonly Dictionary<string, int> _failures = new(StringComparer.Ordinal);
  private readonly Action<string, Exception> _logException;
  private readonly Action<string> _logWarning;
  private bool _stopped;

  public FailureGuard(Action<string, Exception> logException, Action<string> logWarning)
  {
    _logException = logException;
    _logWarning = logWarning;
  }

  /// <summary>The mod is stopping: nothing runs any more.</summary>
  public void Stop() => _stopped = true;

  public void Run(string context, Action action)
  {
    if (_stopped)
      return;

    try
    {
      action();
    }
    catch (Exception exception)
    {
      Report(context, exception);
    }
  }

  public void Run<T>(string context, Action<T> action, T argument)
  {
    if (_stopped)
      return;

    try
    {
      action(argument);
    }
    catch (Exception exception)
    {
      Report(context, exception);
    }
  }

  public void Run<T1, T2>(string context, Action<T1, T2> action, T1 first, T2 second)
  {
    if (_stopped)
      return;

    try
    {
      action(first, second);
    }
    catch (Exception exception)
    {
      Report(context, exception);
    }
  }

  private void Report(string context, Exception exception)
  {
    _failures.TryGetValue(context, out int count);
    _failures[context] = ++count;
    if (count == 1)
      _logException(context, exception);
    else if (count == 10 || count == 100 || count % 1000 == 0)
      _logWarning(
        "[" + context + "] failed " + count + " times. Latest: " + exception.GetType().Name + ": " + exception.Message
      );
  }
}
