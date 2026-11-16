namespace Element.Application.Profiles;

/// <summary>Where the key viewers of the profile coming into use come from.</summary>
internal enum KeyViewersSourceKind
{
  /// <summary>A file ElementNote saved for a profile, or one that came in a profile file.</summary>
  File,

  /// <summary>The key viewers a new ElementNote install starts with.</summary>
  FirstStart,

  /// <summary>The key viewers on screen stay, and now belong to the profile.</summary>
  Keep,
}
