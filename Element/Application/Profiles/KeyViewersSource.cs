namespace Element.Application.Profiles;

/// <summary>What ElementNote shows for the profile coming into use.</summary>
internal sealed class KeyViewersSource
{
  public static readonly KeyViewersSource FirstStart = new(KeyViewersSourceKind.FirstStart, null);
  public static readonly KeyViewersSource Keep = new(KeyViewersSourceKind.Keep, null);

  private KeyViewersSource(KeyViewersSourceKind kind, string path)
  {
    Kind = kind;
    Path = path;
  }

  public KeyViewersSourceKind Kind { get; }

  /// <summary>With <see cref="KeyViewersSourceKind.File"/>: the file's full path.</summary>
  public string Path { get; }

  public static KeyViewersSource File(string path) => new(KeyViewersSourceKind.File, path);
}
