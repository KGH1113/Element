namespace Element.Application.Updates;

internal readonly struct ModUpdateStatus
{
  public ModUpdateStatus(
    ModUpdateState state,
    string version,
    string target = null,
    ModUpdateError error = ModUpdateError.None
  )
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
