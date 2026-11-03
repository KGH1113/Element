using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Element.Application.Files;
using Element.Configuration.Profiles;

namespace Element.Application.Profiles;

/// <summary>
/// Profile actions end to end: the file windows, reading and writing profile
/// files, a settings backup before anything is replaced or removed, applying
/// the new values to the running mod, and saving.
/// </summary>
internal sealed class ProfileService : IProfiles
{
  private const string TypeName = "Element profile";

  private readonly ProfileLibrary _library;
  private readonly IFileDialogs _dialogs;
  private readonly Func<bool> _save;
  private readonly Action _settingsReplaced;
  private readonly Action _backup;
  private readonly string _modVersion;
  private readonly Action<string, Exception> _logError;

  /// <param name="settingsReplaced">Applies the live settings after a switch changed them.</param>
  /// <param name="backup">Copies the current settings aside before an import or a delete.</param>
  public ProfileService(
    ProfileLibrary library,
    IFileDialogs dialogs,
    Func<bool> save,
    Action settingsReplaced,
    Action backup,
    string modVersion,
    Action<string, Exception> logError
  )
  {
    _library = library ?? throw new ArgumentNullException(nameof(library));
    _dialogs = dialogs;
    _save = save ?? (() => true);
    _settingsReplaced = settingsReplaced ?? (() => { });
    _backup = backup ?? (() => { });
    _modVersion = modVersion ?? "";
    _logError = logError ?? ((_, _) => { });
  }

  public IReadOnlyList<ElementProfile> Profiles => _library.Profiles;
  public ElementProfile Active => _library.Active;
  public bool CanDelete => _library.CanDelete;
  public bool FilesSupported => _dialogs != null;

  public ProfileSummary Summarize(ElementProfile profile) => _library.Summarize(profile);

  public bool ValidateName(string name, string exceptId, out ProfileNameProblem problem) =>
    _library.ValidateName(name, exceptId, out _, out problem);

  public ProfileOutcome Switch(string id)
  {
    ElementProfile target = _library.Find(id);
    if (target == null)
      return ProfileOutcome.Fail(ProfileProblem.Missing);
    if (target == _library.Active)
      return ProfileOutcome.Done(target.Name, true);
    try
    {
      _library.Switch(id);
    }
    catch (Exception exception)
    {
      _logError("Switch to profile '" + target.Name + "'", exception);
      return ProfileOutcome.Fail(ProfileProblem.Damaged);
    }
    _settingsReplaced();
    return ProfileOutcome.Done(target.Name, _save());
  }

  public ProfileOutcome Create(string name)
  {
    if (!_library.TryCreate(name, out ElementProfile profile, out ProfileNameProblem problem))
      return ProfileOutcome.InvalidName(problem);
    _settingsReplaced();
    return ProfileOutcome.Done(profile.Name, _save());
  }

  public ProfileOutcome Duplicate(string id, string name)
  {
    ElementProfile source = _library.Find(id);
    if (source == null)
      return ProfileOutcome.Fail(ProfileProblem.Missing);
    ElementProfile copy;
    try
    {
      copy = _library.Duplicate(id, name);
    }
    catch (Exception exception)
    {
      _logError("Duplicate profile '" + source.Name + "'", exception);
      return ProfileOutcome.Fail(ProfileProblem.Damaged);
    }
    return ProfileOutcome.Done(copy.Name, _save());
  }

  public ProfileOutcome Rename(string id, string name)
  {
    if (_library.Find(id) == null)
      return ProfileOutcome.Fail(ProfileProblem.Missing);
    if (!_library.TryRename(id, name, out ProfileNameProblem problem))
      return ProfileOutcome.InvalidName(problem);
    return ProfileOutcome.Done(_library.Find(id).Name, _save());
  }

  public ProfileOutcome Delete(string id)
  {
    ElementProfile profile = _library.Find(id);
    if (profile == null)
      return ProfileOutcome.Fail(ProfileProblem.Missing);
    if (!_library.CanDelete)
      return ProfileOutcome.Fail(ProfileProblem.LastProfile);
    Backup();
    bool switched;
    try
    {
      _library.Delete(id, out switched);
    }
    catch (Exception exception)
    {
      _logError("Delete profile '" + profile.Name + "'", exception);
      return ProfileOutcome.Fail(ProfileProblem.Damaged);
    }
    if (switched)
      _settingsReplaced();
    return ProfileOutcome.Done(profile.Name, _save());
  }

  public ProfileOutcome Import(string dialogTitle)
  {
    FileDialogResult picked = Ask(dialogs =>
      dialogs.Open(
        new FileDialogRequest
        {
          Title = dialogTitle,
          Extension = ProfileFile.Extension,
          AlsoOpens = new[] { ProfileFile.LegacyExtension },
          TypeName = TypeName,
        }
      )
    );
    if (picked.Status != FileDialogStatus.Chosen)
      return DialogClosed(picked);

    ProfileFileResult read = ProfileFileCodec.ReadFile(picked.Path);
    if (!read.Ok)
    {
      if (read.Exception != null)
        _logError("Read profile file " + picked.Path, read.Exception);
      return ProfileOutcome.Fail(Problem(read.Problem), read.File?.ModVersion);
    }
    Backup();
    ElementProfile profile;
    try
    {
      profile = _library.Import(read.File);
    }
    catch (Exception exception)
    {
      _logError("Import profile file " + picked.Path, exception);
      return ProfileOutcome.Fail(ProfileProblem.Damaged);
    }
    _settingsReplaced();
    return ProfileOutcome.Done(profile.Name, _save());
  }

  public ProfileOutcome Export(string id, string dialogTitle)
  {
    ElementProfile profile = _library.Find(id);
    if (profile == null)
      return ProfileOutcome.Fail(ProfileProblem.Missing);
    FileDialogResult picked = Ask(dialogs =>
      dialogs.Save(
        new FileDialogRequest
        {
          Title = dialogTitle,
          Extension = ProfileFile.Extension,
          TypeName = TypeName,
          FileName = FileName(profile.Name),
        }
      )
    );
    if (picked.Status != FileDialogStatus.Chosen)
      return DialogClosed(picked);

    string path = WithExtension(picked.Path);
    ProfileFile file;
    try
    {
      file = _library.Export(id, _modVersion);
    }
    catch (Exception exception)
    {
      _logError("Export profile '" + profile.Name + "'", exception);
      return ProfileOutcome.Fail(ProfileProblem.Damaged);
    }
    Exception failure = ProfileFileCodec.WriteFile(path, file);
    if (failure != null)
    {
      _logError("Write profile file " + path, failure);
      return ProfileOutcome.Fail(ProfileProblem.WriteFailed);
    }
    return ProfileOutcome.Done(profile.Name, true, Path.GetFileName(path));
  }

  /// <summary>A file name that works on macOS and Windows alike, from a profile name.</summary>
  internal static string FileName(string profileName)
  {
    StringBuilder name = new();
    foreach (char c in profileName ?? "")
      name.Append(c < 32 || "<>:\"/\\|?*".IndexOf(c) >= 0 ? '_' : c);
    string trimmed = name.ToString().Trim().TrimEnd('.', ' ');
    return (trimmed.Length == 0 ? "Profile" : trimmed) + "." + ProfileFile.Extension;
  }

  // One extension, however the save window treated the suggested name.
  internal static string WithExtension(string path)
  {
    string extension = "." + ProfileFile.Extension;
    while (path.EndsWith(extension + extension, StringComparison.OrdinalIgnoreCase))
      path = path.Substring(0, path.Length - extension.Length);
    return path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension;
  }

  private FileDialogResult Ask(Func<IFileDialogs, FileDialogResult> show)
  {
    if (_dialogs == null)
      return FileDialogResult.Unavailable(new PlatformNotSupportedException("No system file window on this platform."));
    try
    {
      return show(_dialogs);
    }
    catch (Exception exception)
    {
      return FileDialogResult.Unavailable(exception);
    }
  }

  private ProfileOutcome DialogClosed(FileDialogResult picked)
  {
    if (picked.Status == FileDialogStatus.Cancelled)
      return ProfileOutcome.Fail(ProfileProblem.Cancelled);
    _logError("Open the system file window", picked.Exception);
    return ProfileOutcome.Fail(ProfileProblem.DialogUnavailable);
  }

  // A failed backup is logged and does not stop the action: an import keeps
  // every existing profile, and a delete was asked for explicitly.
  private void Backup()
  {
    try
    {
      _backup();
    }
    catch (Exception exception)
    {
      _logError("Back up settings", exception);
    }
  }

  private static ProfileProblem Problem(ProfileFileProblem problem) =>
    problem switch
    {
      ProfileFileProblem.NotProfile => ProfileProblem.NotProfile,
      ProfileFileProblem.NewerVersion => ProfileProblem.NewerVersion,
      ProfileFileProblem.TooLarge => ProfileProblem.TooLarge,
      ProfileFileProblem.ReadFailed => ProfileProblem.ReadFailed,
      ProfileFileProblem.WriteFailed => ProfileProblem.WriteFailed,
      _ => ProfileProblem.Damaged,
    };
}
