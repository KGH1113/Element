using System.Xml.Serialization;

namespace Element.Configuration.Profiles;

/// <summary>An exported profile: one .eprofile file another player can import.</summary>
[XmlRoot("ElementProfile")]
[CopiedInPlace]
public sealed class ProfileFile
{
  public const int CurrentFormat = 1;
  public const string Extension = "eprofile";

  /// <summary>What files exported by earlier versions end with; they still import.</summary>
  public const string LegacyExtension = "elementprofile";

  public int Format = CurrentFormat;

  /// <summary>The Element version that wrote the file, shown when it is too new to import.</summary>
  public string ModVersion = "";

  public string Name = "";
  public ProfileContent Content;

  /// <summary>
  /// The profile's ElementNote key viewers, every one of them, as ElementNote's
  /// own "Save all" file (JSON). Null when the file came without them; Element
  /// versions before this field skip it.
  /// </summary>
  public string KeyViewers;
}
