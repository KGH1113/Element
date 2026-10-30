using System;
using Element.Domain.Attempts;

namespace Element.Application.Attempts;

/// <summary>
/// Counts a play when it starts, as JipperResourcePack does, and takes the count
/// back when the player leaves before clearing a tile or dying. Counting does not
/// depend on the overlay, so recording mode and co-op still count. Other mods keep
/// a play out of the count through <see cref="AttemptSkipRules"/>.
/// </summary>
internal sealed class AttemptTracker : IAttemptSource, IDisposable
{
  private readonly IAttemptRunSource _runs;
  private readonly IAttemptStore _store;
  private readonly AttemptSkipRules _skipRules;
  private readonly Action<string, Exception> _logError;
  private readonly AttemptBook _book;
  private AttemptRun _run;
  private AttemptCounts _current;
  private bool _running;
  private bool _played;
  private bool _unsaved;

  public AttemptTracker(
    IAttemptRunSource runs,
    IAttemptStore store,
    AttemptSkipRules skipRules,
    Action<string, Exception> logError
  )
  {
    _runs = runs ?? throw new ArgumentNullException(nameof(runs));
    _store = store ?? throw new ArgumentNullException(nameof(store));
    _skipRules = skipRules ?? new AttemptSkipRules();
    _logError = logError;
    _book = store.Load() ?? new AttemptBook();
  }

  public AttemptCounts Read() => _current;

  public void Begin(int startFloor)
  {
    // A start the game never ended (a second start without a hide) is settled first.
    End();
    if (!_runs.TryRead(startFloor, out AttemptRun run))
    {
      _current = default;
      return;
    }

    // Rules are asked only for plays that would count, so autoplay costs nothing.
    if (run.IsCounted && _skipRules.Skips(_logError))
      run = new AttemptRun(run.Map, run.Key, false);
    _run = run;
    _running = true;
    _played = false;
    _current = run.IsCounted ? _book.Add(run.Map, run.Key) : _book.Read(run.Map, run.Key);
  }

  /// <summary>The player cleared a tile or died, so the start was an attempt. Runs on every tile.</summary>
  public void MarkPlayed() => _played = true;

  /// <summary>The play ended. Saving waits until here so play start does no extra work.</summary>
  public void End()
  {
    if (_running)
    {
      _running = false;
      if (_run.IsCounted)
      {
        if (_played)
          _unsaved = true;
        else
          _current = _book.Remove(_run.Map, _run.Key);
      }
    }
    if (!_unsaved)
      return;
    _unsaved = false;
    _store.Save(_book);
  }

  public void Dispose()
  {
    End();
    _store.Flush();
  }
}
