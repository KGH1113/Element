using System;
using Element.Application.Profiles;
using Element.Configuration.Profiles;
using ElementUI;

namespace Element.Presentation.Settings.Profiles;

/// <summary>The small panel under New profile: a name, Create, and why a name can't be used.</summary>
internal sealed class NewProfileViewModel : ViewModel
{
  private readonly IProfiles _profiles;
  private readonly Func<string, string> _create;

  /// <summary>The panel is open.</summary>
  public bool Open;

  public string Name = "";

  // Why the last Create failed, for the name it was given.
  public string Failure = "";
  public string FailedName = "";

  /// <summary>Goes up when the name field should take the keyboard.</summary>
  public int FocusName;

  /// <summary>Why the name can't be used, as it is typed or after Create; empty when it can.</summary>
  public Computed<string> Problem { get; }
  public Computed<bool> CanCreate { get; }
  public Command Create { get; }

  /// <param name="create">Makes the profile; returns null when it did, or why it didn't.</param>
  public NewProfileViewModel(IProfiles profiles, Func<string, string> create)
  {
    _profiles = profiles;
    _create = create;
    Problem = Computed(() => ProblemOf(_profiles, Name, Failure, FailedName));
    CanCreate = Computed(() => Valid(_profiles, Name));
    Create = Command(CreateNow, CanCreate);
  }

  /// <summary>Opens empty.</summary>
  public void Show()
  {
    Name = "";
    Failure = "";
    FailedName = "";
    Open = true;
    FocusName++;
  }

  public void Close() => Open = false;

  /// <summary>Enter in the name field: creates, or says why the name can't be used, even an empty one.</summary>
  public void Submit()
  {
    CreateNow();
    Runtime.Probe();
  }

  private void CreateNow()
  {
    if (!Open)
      return;
    string failure = _create(Name);
    if (failure == null)
    {
      Open = false;
      return;
    }
    Failure = failure;
    FailedName = Name;
    FocusName++;
  }

  // A failure stays until the name changes; then the name is checked as typed.
  [ElementUI.Pure]
  private static string ProblemOf(IProfiles profiles, string name, string failure, string failedName)
  {
    if (failure.Length > 0 && name == failedName)
      return failure;
    if (name.Trim().Length == 0 || profiles.ValidateName(name, null, out ProfileNameProblem problem))
      return "";
    return ProfileProblemText.Name(problem);
  }

  // The profile list changes only by this page's actions, which close the panel.
  [ElementUI.Pure]
  private static bool Valid(IProfiles profiles, string name) => profiles.ValidateName(name, null, out _);
}
