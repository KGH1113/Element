using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

/// <summary>
/// The profiles kept in <see cref="ElementSettings"/>. The live settings always
/// belong to the active profile; switching stores them in that profile and puts
/// the chosen profile's values in their place. No files, no UI.
/// </summary>
internal sealed class ProfileLibrary
{
  private readonly ElementSettings _settings;

  /// <param name="defaultName">The name of the profile made for the settings a player already has.</param>
  public ProfileLibrary(ElementSettings settings, string defaultName)
  {
    _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    Repair(string.IsNullOrWhiteSpace(defaultName) ? "Default" : defaultName.Trim());
  }

  public IReadOnlyList<ElementProfile> Profiles => _settings.Profiles;

  public ElementProfile Active => Find(_settings.ActiveProfileId);

  public ElementProfile Find(string id) =>
    string.IsNullOrEmpty(id) ? null : _settings.Profiles.Find(profile => profile.Id == id);

  public ProfileSummary Summarize(ElementProfile profile)
  {
    if (profile == null)
      return default;
    if (profile.Content == null)
      return new ProfileSummary(_settings.OverlayLayouts.Count, _settings.Effects.Presets.Count);
    return new ProfileSummary(profile.Content.OverlayLayouts?.Count ?? 0, profile.Content.Effects?.Presets?.Count ?? 0);
  }

  public bool ValidateName(string name, string exceptId, out string normalized, out ProfileNameProblem problem)
  {
    normalized = (name ?? "").Trim();
    problem =
      normalized.Length == 0 ? ProfileNameProblem.Empty
      : normalized.Length > ElementProfile.MaximumNameLength ? ProfileNameProblem.TooLong
      : NameTaken(normalized, exceptId) ? ProfileNameProblem.Taken
      : ProfileNameProblem.None;
    return problem == ProfileNameProblem.None;
  }

  /// <summary>Makes <paramref name="id"/> the profile in use. False when it already is or does not exist.</summary>
  /// <exception cref="Exception">The profile's values cannot be used; nothing changed.</exception>
  public bool Switch(string id)
  {
    ElementProfile target = Find(id);
    ElementProfile current = Active;
    if (target == null || target == current)
      return false;
    ElementSettings prepared = target.Content.Prepare();
    current.Content = ProfileContent.Capture(_settings);
    ProfileContent.Apply(prepared, _settings);
    target.Content = null;
    _settings.ActiveProfileId = target.Id;
    return true;
  }

  /// <summary>A new profile with the current values, which becomes the profile in use.</summary>
  public bool TryCreate(string name, out ElementProfile profile, out ProfileNameProblem problem)
  {
    profile = null;
    if (!ValidateName(name, null, out string normalized, out problem))
      return false;
    Active.Content = ProfileContent.Capture(_settings);
    profile = new ElementProfile { Name = normalized };
    _settings.Profiles.Add(profile);
    _settings.ActiveProfileId = profile.Id;
    return true;
  }

  public bool TryRename(string id, string name, out ProfileNameProblem problem)
  {
    ElementProfile profile = Find(id);
    if (!ValidateName(name, id, out string normalized, out problem) || profile == null)
      return false;
    profile.Name = normalized;
    return true;
  }

  public bool CanDelete => _settings.Profiles.Count > 1;

  /// <summary>
  /// Removes a profile. Removing the one in use first switches to its
  /// neighbor, so <paramref name="switched"/> tells the caller to reapply.
  /// </summary>
  public bool Delete(string id, out bool switched)
  {
    switched = false;
    ElementProfile profile = Find(id);
    if (profile == null || !CanDelete)
      return false;
    int index = _settings.Profiles.IndexOf(profile);
    if (profile == Active)
    {
      ElementProfile neighbor = _settings.Profiles[index + 1 < _settings.Profiles.Count ? index + 1 : index - 1];
      switched = Switch(neighbor.Id);
    }
    _settings.Profiles.Remove(profile);
    return true;
  }

  public ProfileFile Export(string id, string modVersion)
  {
    ElementProfile profile = Find(id);
    if (profile == null)
      return null;
    return new ProfileFile
    {
      ModVersion = modVersion ?? "",
      Name = profile.Name,
      Content = profile.Content ?? ProfileContent.Capture(_settings),
    };
  }

  /// <summary>
  /// Adds the file's profile under a free name and switches to it. The profile
  /// that was in use keeps its values, so importing never loses anything.
  /// </summary>
  /// <exception cref="Exception">The file's values cannot be used; nothing changed.</exception>
  public ElementProfile Import(ProfileFile file)
  {
    if (file?.Content == null)
      throw new ArgumentException("The profile file has no values.", nameof(file));
    // Throws before the list changes when the values are unusable.
    file.Content.Prepare();
    ElementProfile profile = new() { Name = FreeName(file.Name), Content = file.Content };
    _settings.Profiles.Add(profile);
    try
    {
      Switch(profile.Id);
    }
    catch
    {
      _settings.Profiles.Remove(profile);
      throw;
    }
    return profile;
  }

  // Keeps the invariants the rest relies on, whatever an older or hand-edited
  // settings file holds: unique ids and names, and exactly one profile in use,
  // which is the one with no stored values.
  private void Repair(string defaultName)
  {
    List<ElementProfile> profiles = _settings.Profiles ??= new List<ElementProfile>();
    HashSet<string> ids = new(StringComparer.Ordinal);
    ElementProfile active = null;
    for (int i = 0; i < profiles.Count; i++)
    {
      ElementProfile profile = profiles[i];
      bool valid = profile != null && !string.IsNullOrEmpty(profile.Id) && ids.Add(profile.Id);
      bool inUse = valid && active == null && profile.Id == _settings.ActiveProfileId;
      // A profile not in use that lost its values has nothing to switch to.
      if (!valid || (!inUse && profile.Content == null))
      {
        profiles.RemoveAt(i--);
        continue;
      }
      if (inUse)
      {
        active = profile;
        // The live settings are this profile's values.
        profile.Content = null;
      }
    }
    if (active == null)
    {
      // Settings from before profiles: what the player has becomes the first profile.
      active = new ElementProfile { Name = defaultName };
      profiles.Insert(0, active);
      _settings.ActiveProfileId = active.Id;
    }
    string[] names = new string[profiles.Count];
    for (int i = 0; i < profiles.Count; i++)
    {
      string name = (profiles[i].Name ?? "").Trim();
      names[i] = name.Length == 0 ? defaultName : name;
      profiles[i].Name = "";
    }
    for (int i = 0; i < profiles.Count; i++)
      profiles[i].Name = FreeName(names[i]);
  }

  // The name itself when free, otherwise the first free "name 2", "name 3"...
  private string FreeName(string name)
  {
    name = (name ?? "").Trim();
    if (name.Length == 0)
      name = "Profile";
    if (name.Length > ElementProfile.MaximumNameLength)
      name = name.Substring(0, ElementProfile.MaximumNameLength).TrimEnd();
    string candidate = name;
    for (int suffix = 2; NameTaken(candidate, null); suffix++)
    {
      string number = " " + suffix;
      candidate = name.Substring(0, Math.Min(name.Length, ElementProfile.MaximumNameLength - number.Length)) + number;
    }
    return candidate;
  }

  private bool NameTaken(string name, string exceptId) =>
    _settings.Profiles.Exists(profile =>
      profile.Id != exceptId && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)
    );
}
