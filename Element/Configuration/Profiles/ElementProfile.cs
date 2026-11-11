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
}
