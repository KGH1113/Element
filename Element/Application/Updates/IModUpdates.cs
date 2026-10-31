namespace Element.Application.Updates;

internal enum ModUpdateState
{
  /// <summary>No launcher decided anything, e.g. a build loaded straight by UMM.</summary>
  Unknown,
  UpToDate,
  /// <summary>This launch switched to <see cref="ModUpdateStatus.Version"/>.</summary>
  Updated,
  /// <summary>Still downloading <see cref="ModUpdateStatus.Target"/>; it applies next launch.</summary>
  Downloading,
  /// <summary><see cref="ModUpdateStatus.Target"/> is downloaded and applies next launch.</summary>
  Ready,
  /// <summary><see cref="ModUpdateStatus.Target"/> failed to start once and is not installed again.</summary>
  Skipped,
  Failed,
  Off,
  Development,
}

internal enum ModUpdateError
{
  None,
  Network,
  Damaged,
  Disk,
  Unknown,
}

internal readonly struct ModUpdateStatus
{
  public ModUpdateStatus(ModUpdateState state, string version, string target = null, ModUpdateError error = ModUpdateError.None)
  {
    State = state;
    Version = version;
    Target = target;
    Error = error;
  }

  public ModUpdateState State { get; }

  /// <summary>The version running now.</summary>
  public string Version { get; }
  public string Target { get; }
  public ModUpdateError Error { get; }
}

/// <summary>Automatic updates as Settings shows and changes them.</summary>
internal interface IModUpdates
{
  ModUpdateStatus Status { get; }

  /// <summary>Takes effect the next time the game starts.</summary>
  bool AutomaticUpdates { get; set; }
}
