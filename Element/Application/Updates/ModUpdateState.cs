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
