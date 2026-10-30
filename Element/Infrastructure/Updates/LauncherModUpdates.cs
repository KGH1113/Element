using System;
using Element.Application.Updates;
using Element.Updates;

namespace Element.Infrastructure.Updates;

/// <summary>Reads what the launcher reported this launch and edits UpdateSettings.json.</summary>
internal sealed class LauncherModUpdates : IModUpdates
{
  private readonly string _modRoot;
  private readonly Action<string, Exception> _logError;
  private bool? _automatic;
  private string _reportJson;
  private ModUpdateStatus _status = new(ModUpdateState.Unknown, null);

  public LauncherModUpdates(string modRoot, Action<string, Exception> logError)
  {
    _modRoot = modRoot;
    _logError = logError ?? ((_, _) => { });
  }

  // The UMM window asks every frame; parse only when the report changed.
  public ModUpdateStatus Status
  {
    get
    {
      string json = UpdateReport.CurrentJson();
      if (ReferenceEquals(json, _reportJson))
        return _status;
      _reportJson = json;
      UpdateReport report = UpdateReport.Parse(json);
      _status =
        report == null
          ? new ModUpdateStatus(ModUpdateState.Unknown, null)
          : new ModUpdateStatus(StateOf(report.Outcome), report.Version, report.Target, ErrorOf(report.Error));
      return _status;
    }
  }

  public bool AutomaticUpdates
  {
    get => _automatic ??= UpdatePreferences.Load(_modRoot).AutomaticUpdates;
    set
    {
      UpdatePreferences preferences = UpdatePreferences.Load(_modRoot);
      preferences.AutomaticUpdates = value;
      try
      {
        preferences.Save(_modRoot);
        _automatic = value;
      }
      catch (Exception exception)
      {
        _logError("Save update preference", exception);
      }
    }
  }

  private static ModUpdateState StateOf(string outcome) =>
    outcome switch
    {
      UpdateOutcomes.UpToDate => ModUpdateState.UpToDate,
      UpdateOutcomes.Updated => ModUpdateState.Updated,
      UpdateOutcomes.Downloading => ModUpdateState.Downloading,
      UpdateOutcomes.Ready => ModUpdateState.Ready,
      UpdateOutcomes.Skipped => ModUpdateState.Skipped,
      UpdateOutcomes.Error => ModUpdateState.Failed,
      UpdateOutcomes.Off => ModUpdateState.Off,
      UpdateOutcomes.Development => ModUpdateState.Development,
      _ => ModUpdateState.Unknown,
    };

  private static ModUpdateError ErrorOf(string error) =>
    error switch
    {
      null => ModUpdateError.None,
      UpdateErrors.Network => ModUpdateError.Network,
      UpdateErrors.Damaged => ModUpdateError.Damaged,
      UpdateErrors.Disk => ModUpdateError.Disk,
      _ => ModUpdateError.Unknown,
    };
}
