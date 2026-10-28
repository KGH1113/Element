using System;
using System.IO;
using System.IO.Compression;

namespace ImplResourcePack.Updater;

/// <summary>Unpacks a verified release zip without letting any entry escape its folder.</summary>
internal static class PackageArchive
{
  public static void Extract(string archivePath, string destination, long maximumBytes)
  {
    Directory.CreateDirectory(destination);
    string root = WithSeparator(Path.GetFullPath(destination));
    byte[] buffer = new byte[81920];
    long written = 0;
    using FileStream file = File.OpenRead(archivePath);
    using ZipArchive archive = new(file, ZipArchiveMode.Read);
    foreach (ZipArchiveEntry entry in archive.Entries)
    {
      string relative = entry.FullName.Replace('\\', '/');
      string target = Path.GetFullPath(Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar)));
      if (!target.StartsWith(root, StringComparison.Ordinal))
        throw new InvalidDataException("The update package contains an unsafe path: " + entry.FullName);
      if (relative.EndsWith("/", StringComparison.Ordinal))
      {
        Directory.CreateDirectory(target);
        continue;
      }
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      using Stream source = entry.Open();
      using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      int read;
      while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
      {
        written += read;
        if (written > maximumBytes)
          throw new InvalidDataException("The update package unpacks to more than its limit.");
        output.Write(buffer, 0, read);
      }
    }
  }

  /// <summary>Resolves <paramref name="relativePath"/> inside <paramref name="root"/>, or throws.</summary>
  public static string Contained(string root, string relativePath)
  {
    string fullRoot = WithSeparator(Path.GetFullPath(root));
    string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    if (!path.StartsWith(fullRoot, StringComparison.Ordinal))
      throw new InvalidDataException("The update manifest runtime path escapes the package.");
    return path;
  }

  private static string WithSeparator(string path) =>
    path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
      ? path
      : path + Path.DirectorySeparatorChar;
}
