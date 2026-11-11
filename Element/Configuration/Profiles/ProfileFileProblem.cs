namespace Element.Configuration.Profiles;

internal enum ProfileFileProblem
{
  None,

  /// <summary>Not an Element profile, such as another program's file.</summary>
  NotProfile,

  /// <summary>Written by a newer Element that this version cannot read safely.</summary>
  NewerVersion,

  /// <summary>An Element profile whose contents are cut off or broken.</summary>
  Damaged,
  TooLarge,
  ReadFailed,
  WriteFailed,
}
