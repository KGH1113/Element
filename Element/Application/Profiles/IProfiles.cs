using System.Collections.Generic;
using System.Threading.Tasks;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

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

  /// <summary>
  /// Asks where to save, then writes the profile there with its ElementNote key viewers.
  /// Those may take ElementNote a moment, so the outcome comes off the main thread.
  /// </summary>
  Task<ProfileOutcome> Export(string id, string dialogTitle);

  /// <summary>The profile whose key viewers ElementNote last failed to show; null once a swap works.</summary>
  string KeyViewersFailedProfile { get; }

  /// <summary>Counts those failures, so a page shows each one once.</summary>
  int KeyViewerFailures { get; }
}
