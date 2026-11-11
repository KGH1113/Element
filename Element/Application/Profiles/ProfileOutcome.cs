namespace Element.Application.Profiles;

/// <summary>What a profile action did, for the settings window to put into words.</summary>
internal readonly struct ProfileOutcome
{
  private ProfileOutcome(ProfileProblem problem, ProfileNameProblem nameProblem, string name, string detail, bool saved)
  {
    Problem = problem;
    NameProblem = nameProblem;
    Name = name;
    Detail = detail;
    Saved = saved;
  }

  public ProfileProblem Problem { get; }
  public ProfileNameProblem NameProblem { get; }

  /// <summary>The profile acted on.</summary>
  public string Name { get; }

  /// <summary>Export: the file name written. NewerVersion: the Element version that wrote the file.</summary>
  public string Detail { get; }

  /// <summary>False when the change applied but the settings file could not be written.</summary>
  public bool Saved { get; }

  public bool Ok => Problem == ProfileProblem.None;

  public static ProfileOutcome Done(string name, bool saved, string detail = null) =>
    new(ProfileProblem.None, ProfileNameProblem.None, name, detail, saved);

  public static ProfileOutcome Fail(ProfileProblem problem, string detail = null) =>
    new(problem, ProfileNameProblem.None, null, detail, true);

  public static ProfileOutcome InvalidName(ProfileNameProblem problem) =>
    new(ProfileProblem.Name, problem, null, null, true);
}
