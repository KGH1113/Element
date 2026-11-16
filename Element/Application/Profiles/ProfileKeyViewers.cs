using System;
using System.Threading.Tasks;
using Element.Configuration;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

/// <summary>
/// Each profile's ElementNote key viewers. ElementNote shows one profile's key viewers and
/// says whose; every other profile's wait in a file beside the settings. Whenever the
/// profile in use isn't the one on screen, ElementNote saves the screen's key viewers to
/// their profile's file and shows the profile in use's.
/// </summary>
/// <remarks>
/// ElementNote remembers whose key viewers it shows in the same save as the key viewers
/// themselves, so a game that quits halfway through a swap can't file one profile's key
/// viewers under another. A profile's file exists only while ElementNote doesn't show it.
/// Main thread; ElementNote does the heavy work in its own process.
/// </remarks>
internal sealed class ProfileKeyViewers
{
  private readonly Func<ElementSettings> _settings;
  private readonly IElementNoteKeyViewers _app;
  private readonly IProfileKeyViewerFiles _files;
  private readonly Action<string, Exception> _logError;
  private readonly Action<string> _logWarning;
  private bool _dirty = true;
  private Swap _swap;

  public ProfileKeyViewers(
    Func<ElementSettings> settings,
    IElementNoteKeyViewers app,
    IProfileKeyViewerFiles files,
    Action<string, Exception> logError,
    Action<string> logWarning
  )
  {
    _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    _app = app ?? throw new ArgumentNullException(nameof(app));
    _files = files ?? throw new ArgumentNullException(nameof(files));
    _logError = logError ?? ((_, _) => { });
    _logWarning = logWarning ?? (_ => { });
    _app.Changed += Sync;
  }

  /// <summary>The profile whose key viewers ElementNote last failed to show; null once a swap works.</summary>
  public string FailedProfileName { get; private set; }

  /// <summary>Counts failed swaps, so a page can tell a new failure from one it already showed.</summary>
  public int Failures { get; private set; }

  /// <summary>The profile in use may have changed: ElementNote shows its key viewers on a coming frame.</summary>
  public void Sync() => _dirty = true;

  /// <summary>Every frame. Finishes a swap ElementNote answered and starts the next one.</summary>
  public void Tick()
  {
    if (_swap != null)
    {
      if (!_swap.Task.IsCompleted)
        return;
      Finish(_swap);
      _swap = null;
    }
    if (!_dirty)
      return;
    _dirty = false;
    try
    {
      Start();
    }
    catch (Exception exception)
    {
      _logError("Switch ElementNote's key viewers", exception);
    }
  }

  // ---- What profile actions do to the key viewers ----

  /// <summary>A new profile starts with the key viewers of a new ElementNote install.</summary>
  public void Created(ElementProfile profile)
  {
    profile.KeyViewersStart = KeyViewersStart.FirstStart;
    profile.KeyViewersCopyOf = null;
    Sync();
  }

  /// <summary>A duplicate starts with the key viewers of the profile it copies.</summary>
  public void Duplicated(ElementProfile source, ElementProfile copy)
  {
    copy.KeyViewersStart = source.KeyViewersStart;
    copy.KeyViewersCopyOf = source.KeyViewersCopyOf;
    try
    {
      bool showing = Showing(source.Id);
      if (!showing && !Leaving(source.Id) && _files.Exists(source.Id))
      {
        _files.Copy(source.Id, copy.Id);
        return;
      }
      // Never shown yet: the copy starts the same way.
      if (!showing && source.KeyViewersStart != KeyViewersStart.Current)
        return;
      if (showing)
        SaveOnScreenFor(copy);
      // On screen now, in a file soon, or out of reach: the copy follows the source.
      if (showing || Leaving(source.Id) || !_app.Connected)
      {
        copy.KeyViewersStart = KeyViewersStart.CopyOf;
        copy.KeyViewersCopyOf = source.Id;
      }
    }
    catch (Exception exception)
    {
      _logError("Copy the key viewers of profile '" + source.Name + "'", exception);
    }
  }

  /// <summary>
  /// Before a profile is removed: its key viewers go to Backups, after the profiles that
  /// were to start as copies of them get theirs.
  /// </summary>
  public void Deleting(ElementProfile profile)
  {
    try
    {
      foreach (ElementProfile other in _settings().Profiles)
      {
        if (other == profile || other.KeyViewersStart != KeyViewersStart.CopyOf || other.KeyViewersCopyOf != profile.Id)
          continue;
        if (Showing(profile.Id))
          SaveOnScreenFor(other);
        else if (_files.Exists(profile.Id))
          _files.Copy(profile.Id, other.Id);
        else
        {
          other.KeyViewersStart = profile.KeyViewersStart;
          other.KeyViewersCopyOf = profile.KeyViewersCopyOf;
        }
      }
      if (_files.Exists(profile.Id))
        _files.BackUp(profile.Id);
      // Shown on screen, they go to Backups when ElementNote swaps them out.
    }
    catch (Exception exception)
    {
      _logError("Back up the key viewers of profile '" + profile.Name + "'", exception);
    }
    Sync();
  }

  /// <summary>An imported profile brings its key viewers, or starts with the ones on screen.</summary>
  public void Imported(ElementProfile profile, string keyViewers)
  {
    profile.KeyViewersStart = KeyViewersStart.Current;
    profile.KeyViewersCopyOf = null;
    if (!string.IsNullOrEmpty(keyViewers))
    {
      try
      {
        _files.Write(profile.Id, keyViewers);
      }
      catch (Exception exception)
      {
        _logError("Keep the key viewers of imported profile '" + profile.Name + "'", exception);
      }
    }
    Sync();
  }

  /// <summary>A profile's key viewers for its exported file. Completes off the main thread.</summary>
  public Task<KeyViewersExport> Export(ElementProfile profile)
  {
    if (Leaving(profile.Id))
    {
      // The swap under way saves them to the profile's file first.
      string id = profile.Id;
      return _swap.Task.ContinueWith(_ => ReadOwn(id, profile.Name), TaskScheduler.Default);
    }
    if (Showing(profile.Id))
    {
      string path = _files.ExportPathOf(profile.Id);
      return Guarded(() => _app.SaveAsync(path))
        .ContinueWith(
          saved =>
          {
            try
            {
              return saved.Result ? KeyViewersExport.Of(_files.Read(path)) : KeyViewersExport.Missed;
            }
            catch (Exception exception)
            {
              _logError("Read the key viewers ElementNote saved for export", exception);
              return KeyViewersExport.Missed;
            }
            finally
            {
              TryDelete(path);
            }
          },
          TaskScheduler.Default
        );
    }
    string own = OwnFile(profile);
    if (own != null)
    {
      return Task.Run(() =>
      {
        try
        {
          return KeyViewersExport.Of(_files.ReadToShare(own));
        }
        catch (Exception exception)
        {
          _logError("Read the key viewers of profile '" + profile.Name + "'", exception);
          return KeyViewersExport.Missed;
        }
      });
    }
    // Without ElementNote, a profile with nothing saved most likely has them on its screen.
    bool missed = profile.KeyViewersStart != KeyViewersStart.FirstStart && !_app.Connected;
    return Task.FromResult(missed ? KeyViewersExport.Missed : KeyViewersExport.None);
  }

  // ---- Swapping ----

  private sealed class Swap
  {
    public Task<bool> Task;
    public string ProfileId;
    public string ProfileName;

    // Whose key viewers the swap was planned on.
    public string ExpectedOwner;

    // The profile file ElementNote loaded, now on screen and stale.
    public string LoadedFile;
  }

  private void Start()
  {
    if (!_app.Connected)
      return;
    ElementSettings settings = _settings();
    ElementProfile active = Find(settings, settings.ActiveProfileId);
    if (active == null)
      return;
    string owner = _app.Owner;
    if (owner == active.Id)
    {
      Settle(active);
      return;
    }
    KeyViewersSource load = SourceFor(active, owner);
    string save =
      owner == null
        // Key viewers that belong to no profile yet: the ones before this feature stay with
        // the profile in use, and anything else coming in keeps them in Backups.
        ? (load.Kind == KeyViewersSourceKind.Keep ? null : _files.NewBackupPath())
      : Find(settings, owner) != null ? _files.PathOf(owner)
      : _files.NewBackupPath();
    _swap = new Swap
    {
      Task = Guarded(() => _app.SwapAsync(save, load, active.Id, owner)),
      ProfileId = active.Id,
      ProfileName = active.Name,
      ExpectedOwner = owner,
      LoadedFile = load.Kind == KeyViewersSourceKind.File && load.Path == _files.PathOf(active.Id) ? load.Path : null,
    };
  }

  private void Finish(Swap swap)
  {
    bool shown = swap.Task.Status == TaskStatus.RanToCompletion && swap.Task.Result;
    // ElementNote showed someone else's than planned and said whose: plan again, quietly.
    if (!shown && _app.Connected && _app.Owner != swap.ExpectedOwner)
    {
      Sync();
      return;
    }
    if (!shown)
    {
      FailedProfileName = swap.ProfileName;
      Failures++;
      _logWarning(
        "ElementNote didn't switch to the key viewers of profile '"
          + swap.ProfileName
          + "'. Element asks again when a profile is chosen or ElementNote reconnects."
      );
      return;
    }
    FailedProfileName = null;
    if (swap.LoadedFile != null)
      TryDelete(swap.LoadedFile);
    ElementProfile profile = Find(_settings(), swap.ProfileId);
    if (profile != null)
      Settle(profile);
  }

  // The profile is on screen: whatever it was to start with no longer matters, and a file
  // left from a swap the game didn't see finish is stale.
  private void Settle(ElementProfile profile)
  {
    profile.KeyViewersStart = KeyViewersStart.Current;
    profile.KeyViewersCopyOf = null;
    if (_files.Exists(profile.Id))
      TryDelete(_files.PathOf(profile.Id));
  }

  private KeyViewersSource SourceFor(ElementProfile profile, string owner)
  {
    if (_files.Exists(profile.Id))
      return KeyViewersSource.File(_files.PathOf(profile.Id));
    switch (profile.KeyViewersStart)
    {
      case KeyViewersStart.FirstStart:
        return KeyViewersSource.FirstStart;
      case KeyViewersStart.CopyOf:
        string source = profile.KeyViewersCopyOf;
        if (source != null && source != profile.Id && source != owner && _files.Exists(source))
          return KeyViewersSource.File(_files.PathOf(source));
        // On screen, the source's key viewers are saved to its file as the copy keeps them.
        return KeyViewersSource.Keep;
      default:
        return KeyViewersSource.Keep;
    }
  }

  private string OwnFile(ElementProfile profile)
  {
    if (_files.Exists(profile.Id))
      return _files.PathOf(profile.Id);
    string source = profile.KeyViewersCopyOf;
    return
      profile.KeyViewersStart == KeyViewersStart.CopyOf
      && source != null
      && source != profile.Id
      && _files.Exists(source)
      ? _files.PathOf(source)
      : null;
  }

  // On screen, and not on its way off.
  private bool Showing(string profileId) => _app.Connected && _app.Owner == profileId && !Leaving(profileId);

  // On screen while a swap to another profile saves it to its file.
  private bool Leaving(string profileId) => _swap != null && _swap.ProfileId != profileId && _app.Owner == profileId;

  private KeyViewersExport ReadOwn(string profileId, string name)
  {
    try
    {
      return _files.Exists(profileId)
        ? KeyViewersExport.Of(_files.ReadToShare(_files.PathOf(profileId)))
        : KeyViewersExport.Missed;
    }
    catch (Exception exception)
    {
      _logError("Read the key viewers of profile '" + name + "'", exception);
      return KeyViewersExport.Missed;
    }
  }

  // ElementNote answers in its own time; if it fails the copy still follows the source.
  private void SaveOnScreenFor(ElementProfile copy)
  {
    string name = copy.Name;
    Guarded(() => _app.SaveAsync(_files.PathOf(copy.Id)))
      .ContinueWith(
        saved =>
        {
          if (!saved.Result)
            _logWarning("ElementNote didn't save key viewers for profile '" + name + "'.");
        },
        TaskScheduler.Default
      );
  }

  private Task<bool> Guarded(Func<Task<bool>> ask)
  {
    try
    {
      return ask() ?? Task.FromResult(false);
    }
    catch (Exception exception)
    {
      _logError("Ask ElementNote about key viewers", exception);
      return Task.FromResult(false);
    }
  }

  private void TryDelete(string path)
  {
    try
    {
      _files.Delete(path);
    }
    catch (Exception exception)
    {
      _logError("Remove the key viewer file " + path, exception);
    }
  }

  private static ElementProfile Find(ElementSettings settings, string id) =>
    string.IsNullOrEmpty(id) ? null : settings.Profiles.Find(profile => profile.Id == id);
}
