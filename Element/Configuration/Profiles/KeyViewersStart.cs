namespace Element.Configuration.Profiles;

/// <summary>
/// What ElementNote shows for a profile that has no key viewers of its own yet,
/// the first time the profile is used.
/// </summary>
public enum KeyViewersStart
{
  /// <summary>The key viewers on screen then; a profile from before key viewers came with profiles.</summary>
  Current,

  /// <summary>The key viewers a new ElementNote install starts with; a new profile.</summary>
  FirstStart,

  /// <summary>A copy of another profile's; a duplicate made while those were out of reach.</summary>
  CopyOf,
}
