using System;
using System.IO;
using Element.Application.Profiles;
using Element.Configuration.Profiles;

namespace Element.Infrastructure.Storage;

/// <summary>Profile files on disk.</summary>
internal sealed class ProfileFileStore : IProfileFiles
{
  /// <summary>
  /// Far above any real profile, key viewer images included; keeps a wrong
  /// pick, such as a video, out of memory.
  /// </summary>
  public const long MaximumBytes = 64L * 1024 * 1024;

  public ProfileFileResult Read(string path)
  {
    try
    {
      FileInfo info = new(path);
      if (!info.Exists)
        return ProfileFileResult.Fail(ProfileFileProblem.ReadFailed, new FileNotFoundException(path));
      if (info.Length > MaximumBytes)
        return ProfileFileResult.Fail(ProfileFileProblem.TooLarge);
      using FileStream stream = info.OpenRead();
      return ProfileFileCodec.Read(stream);
    }
    catch (Exception exception)
      when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
    {
      return ProfileFileResult.Fail(ProfileFileProblem.ReadFailed, exception);
    }
  }

  // Writes beside the destination first and moves it into place.
  public Exception Write(string path, ProfileFile file)
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
        ProfileFileCodec.Write(stream, file);
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
