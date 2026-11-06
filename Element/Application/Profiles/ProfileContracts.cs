using System.Collections.Generic;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

internal enum ProfileNameProblem
{
  None,
  Empty,
  TooLong,
  Taken,
}

/// <summary>What the profile list shows for one profile.</summary>
internal readonly struct ProfileSummary
{
  public ProfileSummary(int elements, int presets)
  {
    Elements = elements;
    Presets = presets;
  }

  public int Elements { get; }
  public int Presets { get; }
}

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

/// <summary>Profiles as the settings window sees them.</summary>
internal interface IProfiles
{
  IReadOnlyList<ElementProfile> Profiles { get; }
  ElementProfile Active { get; }
  bool CanDelete { get; }

  /// <summary>False where the mod has no system file window, so import and export are hidden.</summary>
  bool FilesSupported { get; }
  ProfileSummary Summarize(ElementProfile profile);
  bool ValidateName(string name, string exceptId, out ProfileNameProblem problem);
  ProfileOutcome Switch(string id);

  /// <summary>A new profile with a first start's values, which becomes the profile in use.</summary>
  ProfileOutcome Create(string name);

  /// <summary>A copy of a profile next to it; the profile in use stays.</summary>
  ProfileOutcome Duplicate(string id, string name);
  ProfileOutcome Rename(string id, string name);
  ProfileOutcome Delete(string id);

  /// <summary>Asks for a profile file, then adds it and switches to it.</summary>
  ProfileOutcome Import(string dialogTitle);

  /// <summary>Asks where to save, then writes the profile there.</summary>
  ProfileOutcome Export(string id, string dialogTitle);
}
