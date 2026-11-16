namespace Element.Application.Profiles;

internal enum ProfileProblem
{
  None,

  /// <summary>The player closed the file window; nothing to report.</summary>
  Cancelled,
  Name,
  Missing,
  LastProfile,
  NotProfile,
  NewerVersion,
  Damaged,
  TooLarge,
  ReadFailed,
  WriteFailed,
  DialogUnavailable,
}
