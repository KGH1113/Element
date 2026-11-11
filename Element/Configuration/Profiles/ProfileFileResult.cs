using System;

namespace Element.Configuration.Profiles;

internal readonly struct ProfileFileResult
{
  public ProfileFileResult(ProfileFile file, ProfileFileProblem problem, Exception exception = null)
  {
    File = file;
    Problem = problem;
    Exception = exception;
  }

  public ProfileFile File { get; }
  public ProfileFileProblem Problem { get; }
  public Exception Exception { get; }
  public bool Ok => Problem == ProfileFileProblem.None;

  public static ProfileFileResult Fail(
    ProfileFileProblem problem,
    Exception exception = null,
    ProfileFile file = null
  ) => new(file, problem, exception);
}
