using System;
using System.IO;
using System.Threading;
using ImplResourcePack.Application.Attempts;
using ImplResourcePack.Domain.Attempts;
using ImplResourcePack.Updates;

namespace ImplResourcePack.Infrastructure.Attempts;

/// <summary>
/// Keeps attempts in Attempts.dat at the mod root, which updates leave alone.
/// The book is encoded on the caller's thread and written on a worker, so a
/// slow disk never stalls the game; back-to-back saves write only the latest.
/// </summary>
internal sealed class AttemptFileStore : IAttemptStore
{
  public const string FileName = "Attempts.dat";
  private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(3);

  private readonly string _path;
  private readonly Action<string, Exception> _logError;
  private readonly Action<string> _logWarning;
  private readonly object _gate = new();
  private byte[] _pending;
  private bool _writing;
  private bool _readOnly;

  public AttemptFileStore(string directory, Action<string, Exception> logError, Action<string> logWarning)
  {
    _path = Path.Combine(directory, FileName);
    _logError = logError ?? ((_, _) => { });
    _logWarning = logWarning ?? (_ => { });
  }

  public AttemptBook Load()
  {
    if (!File.Exists(_path))
      return new AttemptBook();
    try
    {
      return AttemptBookCodec.Decode(File.ReadAllBytes(_path));
    }
    catch (Exception exception)
    {
      string kept = SetAside();
      _logError(
        kept != null
          ? FileName + " could not be read, so attempts count from zero. The old file is kept as " + kept
          : FileName + " could not be read or copied aside, so attempts are not saved until the game restarts",
        exception
      );
      return new AttemptBook();
    }
  }

  public void Save(AttemptBook book)
  {
    if (_readOnly)
      return;
    byte[] data = AttemptBookCodec.Encode(book);
    lock (_gate)
    {
      _pending = data;
      if (_writing)
        return;
      _writing = true;
    }
    ThreadPool.QueueUserWorkItem(_ => WritePending());
  }

  public void Flush()
  {
    DateTime deadline = DateTime.UtcNow + FlushTimeout;
    lock (_gate)
    {
      while (_writing)
      {
        TimeSpan left = deadline - DateTime.UtcNow;
        if (left <= TimeSpan.Zero || !Monitor.Wait(_gate, left))
        {
          _logWarning("Attempt counts were still saving when the mod stopped; the last play may not be counted.");
          return;
        }
      }
    }
  }

  private void WritePending()
  {
    while (true)
    {
      byte[] data;
      lock (_gate)
      {
        data = _pending;
        _pending = null;
        if (data == null)
        {
          _writing = false;
          Monitor.PulseAll(_gate);
          return;
        }
      }
      try
      {
        AtomicFile.WriteAllBytes(_path, data);
      }
      catch (Exception exception)
      {
        _logError("Save " + FileName, exception);
      }
    }
  }

  // Keeps an unreadable file so the next save cannot overwrite the old counts;
  // without a copy, saving stays off.
  private string SetAside()
  {
    string kept = _path + ".broken";
    try
    {
      File.Copy(_path, kept, true);
      return Path.GetFileName(kept);
    }
    catch (Exception)
    {
      _readOnly = true;
      return null;
    }
  }
}
