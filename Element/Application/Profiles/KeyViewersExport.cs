namespace Element.Application.Profiles;

/// <summary>A profile's key viewers for its exported file.</summary>
internal readonly struct KeyViewersExport
{
  private KeyViewersExport(string json, bool leftOut)
  {
    Json = json;
    LeftOut = leftOut;
  }

  /// <summary>ElementNote's "Save all" file, or null when the profile has none to include.</summary>
  public string Json { get; }

  /// <summary>The profile has key viewers, but ElementNote wasn't there to save them.</summary>
  public bool LeftOut { get; }

  public static readonly KeyViewersExport None = new(null, false);
  public static readonly KeyViewersExport Missed = new(null, true);

  public static KeyViewersExport Of(string json) => new(json, false);
}
