namespace Element.Application.Profiles;

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
