using System;
using System.Linq;
using Element.Updates;
using UnityModManagerNet;

namespace Element.Launcher;

/// <summary>
/// The UMM entry point. Picks the version to run, lets the running version's
/// updater install a newer one, and starts the newer one in this launch when
/// it is ready in time. A version that fails to start is rolled back and skipped.
/// </summary>
public static class Entry
{
  public static bool Load(UnityModManager.ModEntry modEntry)
  {
    string displayName = modEntry.Info.DisplayName;
    RuntimeStore store = new(modEntry.Path);
    RuntimeState state;
    RuntimeCandidate current;
    try
    {
      state = store.LoadAndRepair();
      current = store.Get(state.Current);
    }
    catch (Exception exception)
    {
      modEntry.Info.DisplayName = displayName + " <color=red>[Could not start - reinstall Element]</color>";
      Warn(modEntry, "The installed runtime is damaged.", exception);
      return false;
    }
    modEntry.Info.Version = current.Version;

    RuntimeCandidate update;
    try
    {
      update = FindUpdate(modEntry, store, state, current);
    }
    catch (Exception exception)
    {
      // Checking for updates must never keep the installed version from starting.
      Warn(modEntry, "The update check failed.", exception);
      UpdateReport.Of(UpdateOutcomes.Error, current.Version, error: UpdateErrors.Unknown).Publish();
      update = null;
    }
    if (update != null)
      return StartUpdate(modEntry, store, state, current, update, displayName);
    return StartCurrent(modEntry, current, displayName);
  }

  private static bool StartCurrent(UnityModManager.ModEntry modEntry, RuntimeCandidate current, string displayName)
  {
    modEntry.Info.Version = current.Version;
    if (PayloadLoader.TryLoad(modEntry, current, out Exception failure))
    {
      // Repairs a launcher switch an earlier launch could not finish.
      TryCommitLauncher(modEntry, current);
      return true;
    }
    modEntry.Info.DisplayName = displayName + " <color=red>[Could not start - see the log]</color>";
    Warn(modEntry, current.Version + " failed to start.", failure);
    return false;
  }

  private static RuntimeCandidate FindUpdate(
    UnityModManager.ModEntry modEntry,
    RuntimeStore store,
    RuntimeState state,
    RuntimeCandidate current
  )
  {
    if (current.IsDevelopment)
    {
      UpdateReport.Of(UpdateOutcomes.Development, current.Version).Publish();
      return null;
    }
    UpdatePreferences preferences = UpdatePreferences.Load(modEntry.Path);
    if (!preferences.AutomaticUpdates)
    {
      UpdateReport.Of(UpdateOutcomes.Off, current.Version).Publish();
      return null;
    }

    UpdaterResponse response;
    try
    {
      response = UpdaterLoader.Resolve(modEntry, current, state.Failed, preferences.LocalSource());
    }
    catch (Exception exception)
    {
      Warn(modEntry, "The updater could not run.", exception);
      UpdateReport.Of(UpdateOutcomes.Error, current.Version, error: UpdateErrors.Unknown).Publish();
      return null;
    }

    switch (response.Outcome)
    {
      case UpdaterResponse.Candidate:
        return OpenCandidate(modEntry, store, state, current, response);
      case UpdaterResponse.Deferred:
        // The updater reports the download itself until it finishes.
        return null;
      case UpdaterResponse.Skipped:
        UpdateReport.Of(UpdateOutcomes.Skipped, current.Version, response.Version).Publish();
        return null;
      case UpdaterResponse.Error:
        UpdateReport.Of(UpdateOutcomes.Error, current.Version, error: response.ErrorKind).Publish();
        return null;
      default:
        UpdateReport.Of(UpdateOutcomes.UpToDate, current.Version).Publish();
        return null;
    }
  }

  private static RuntimeCandidate OpenCandidate(
    UnityModManager.ModEntry modEntry,
    RuntimeStore store,
    RuntimeState state,
    RuntimeCandidate current,
    UpdaterResponse response
  )
  {
    try
    {
      if (
        !SemanticVersion.TryParse(response.Version, out SemanticVersion version)
        || version.CompareTo(SemanticVersion.Parse(current.Version)) <= 0
        || state.Failed.Any(item => SemanticVersion.Same(item, response.Version))
      )
        throw new InvalidOperationException("The updater offered an unusable version: " + response.Version);
      return store.Open(response.Version, response.RuntimePath);
    }
    catch (Exception exception)
    {
      Warn(modEntry, "The downloaded update is not usable.", exception);
      UpdateReport.Of(UpdateOutcomes.Error, current.Version, response.Version, UpdateErrors.Damaged).Publish();
      return null;
    }
  }

  private static bool StartUpdate(
    UnityModManager.ModEntry modEntry,
    RuntimeStore store,
    RuntimeState state,
    RuntimeCandidate current,
    RuntimeCandidate update,
    string displayName
  )
  {
    try
    {
      state.Trial = update.Version;
      store.Save(state);
    }
    catch (Exception exception)
    {
      // Without a trial record a crash could not be rolled back; start the current version instead.
      state.Trial = null;
      Warn(modEntry, "Runtime/state.json could not be written; " + update.Version + " waits for the next launch.", exception);
      UpdateReport.Of(UpdateOutcomes.Error, current.Version, update.Version, UpdateErrors.Disk).Publish();
      return StartCurrent(modEntry, current, displayName);
    }
    modEntry.Info.Version = update.Version;
    UpdateReport.Of(UpdateOutcomes.Updated, update.Version).Publish();
    if (PayloadLoader.TryLoad(modEntry, update, out Exception failure))
    {
      try
      {
        store.Promote(state, update.Version);
        // Only a recorded promotion may switch UMM to the new launcher; otherwise
        // the next launch would read the trial as a crash and drop a working version.
        TryCommitLauncher(modEntry, update);
      }
      catch (Exception exception)
      {
        Warn(modEntry, update.Version + " started, but the update record could not be saved.", exception);
      }
      modEntry.Logger.Log("[Update] Updated " + current.Version + " to " + update.Version + ".");
      return true;
    }

    // The failed version may have patched the game partway; the old one
    // starts cleanly next launch instead of loading beside it now.
    state.Trial = null;
    RuntimeStore.MarkFailed(state, update.Version);
    try
    {
      store.Save(state);
      store.DeleteUnreferenced(update.Version, state);
    }
    catch (Exception exception)
    {
      Warn(modEntry, "The failed update could not be recorded; the next launch retries it once.", exception);
    }
    modEntry.Info.Version = current.Version;
    modEntry.Info.DisplayName = displayName + " <color=red>[Update failed - restart the game]</color>";
    Warn(
      modEntry,
      update.Version + " failed to start. Restart the game to keep using " + current.Version
        + "; " + update.Version + " will not be installed again.",
      failure
    );
    return false;
  }

  private static void TryCommitLauncher(UnityModManager.ModEntry modEntry, RuntimeCandidate runtime)
  {
    try
    {
      LauncherInfo.Commit(modEntry.Path, runtime);
    }
    catch (Exception exception)
    {
      Warn(modEntry, "The launcher for " + runtime.Version + " could not be selected; the next launch retries.", exception);
    }
  }

  private static void Warn(UnityModManager.ModEntry modEntry, string message, Exception exception) =>
    modEntry.Logger.Warning("[Update] " + message + (exception == null ? "" : " " + exception));
}
