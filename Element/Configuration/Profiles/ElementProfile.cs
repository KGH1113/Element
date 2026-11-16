using System;

namespace Element.Configuration.Profiles;

/// <summary>A named set of settings the player switches between and shares as a file.</summary>
[CopiedInPlace]
public sealed class ElementProfile
{
  public const int MaximumNameLength = 40;

  public string Id = Guid.NewGuid().ToString("N");
  public string Name = "";

  /// <summary>
  /// The saved values of a profile that is not in use. Null for the profile in
  /// use, whose values are the live settings.
  /// </summary>
  public ProfileContent Content;

  /// <summary>
  /// What ElementNote shows the first time this profile is used, while it has
  /// no key viewers of its own. Each profile's key viewers are ElementNote's,
  /// kept in a file beside the settings while another profile is in use.
  /// </summary>
  public KeyViewersStart KeyViewersStart = KeyViewersStart.Current;

  /// <summary>With <see cref="Profiles.KeyViewersStart.CopyOf"/>: the profile whose key viewers this one starts with.</summary>
  public string KeyViewersCopyOf;
}
