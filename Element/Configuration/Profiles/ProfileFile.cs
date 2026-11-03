using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Element.Configuration.Profiles;

/// <summary>An exported profile: one .elementprofile file another player can import.</summary>
[XmlRoot("ElementProfile")]
public sealed class ProfileFile
{
  public const int CurrentFormat = 1;
  public const string Extension = "elementprofile";

  public int Format = CurrentFormat;

  /// <summary>The Element version that wrote the file, shown when it is too new to import.</summary>
  public string ModVersion = "";

  public string Name = "";
  public ProfileContent Content;
}

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

/// <summary>Reads and writes profile files. Reading never trusts the file: no DTDs, no outside references.</summary>
internal static class ProfileFileCodec
{
  /// <summary>Far above any real profile; keeps a wrong pick, such as a video, out of memory.</summary>
  public const long MaximumBytes = 16L * 1024 * 1024;

  public static void Write(Stream stream, ProfileFile file)
  {
    using XmlWriter writer = XmlWriter.Create(
      stream,
      new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }
    );
    new XmlSerializer(typeof(ProfileFile)).Serialize(writer, file);
  }

  public static ProfileFileResult Read(Stream stream)
  {
    ProfileFile file;
    try
    {
      using XmlReader reader = XmlReader.Create(
        stream,
        new XmlReaderSettings
        {
          // Overlay texts may be only spaces; see SettingsXml.
          IgnoreWhitespace = false,
          DtdProcessing = DtdProcessing.Prohibit,
          XmlResolver = null,
        }
      );
      XmlSerializer serializer = new(typeof(ProfileFile));
      if (!serializer.CanDeserialize(reader))
        return ProfileFileResult.Fail(ProfileFileProblem.NotProfile);
      file = serializer.Deserialize(reader) as ProfileFile;
    }
    catch (XmlException exception)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.NotProfile, exception);
    }
    catch (InvalidOperationException exception)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.Damaged, exception);
    }
    if (file == null)
      return ProfileFileResult.Fail(ProfileFileProblem.NotProfile);
    if (
      file.Format > ProfileFile.CurrentFormat
      || file.Content?.SettingsVersion > ElementSettings.CurrentSettingsVersion
    )
      return ProfileFileResult.Fail(ProfileFileProblem.NewerVersion, file: file);
    if (file.Content == null)
      return ProfileFileResult.Fail(ProfileFileProblem.Damaged, file: file);
    return new ProfileFileResult(file, ProfileFileProblem.None);
  }

  public static ProfileFileResult ReadFile(string path)
  {
    try
    {
      FileInfo info = new(path);
      if (!info.Exists)
        return ProfileFileResult.Fail(ProfileFileProblem.ReadFailed, new FileNotFoundException(path));
      if (info.Length > MaximumBytes)
        return ProfileFileResult.Fail(ProfileFileProblem.TooLarge);
      using FileStream stream = info.OpenRead();
      return Read(stream);
    }
    catch (Exception exception)
      when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.ReadFailed, exception);
    }
  }

  /// <summary>
  /// Writes beside the destination first and moves it into place, so a failed
  /// write leaves an existing file whole. Returns null on success.
  /// </summary>
  public static Exception WriteFile(string path, ProfileFile file)
  {
    string temporary = null;
    try
    {
      string directory = Path.GetDirectoryName(Path.GetFullPath(path));
      temporary = Path.Combine(
        directory ?? ".",
        "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp"
      );
      using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write))
        Write(stream, file);
      if (File.Exists(path))
        File.Delete(path);
      File.Move(temporary, path);
      temporary = null;
      return null;
    }
    catch (Exception exception)
      when (exception
          is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or InvalidOperationException
      )
    {
      return exception;
    }
    finally
    {
      if (temporary != null)
      {
        try
        {
          File.Delete(temporary);
        }
        catch (Exception) { }
      }
    }
  }
}
