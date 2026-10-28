using System;
using System.IO;
using System.Text;

namespace ImplResourcePack.Updates;

/// <summary>
/// Writes that never leave a half-written file, and never touch an unchanged
/// one: a DLL another mod has loaded stays locked on Windows.
/// </summary>
internal static class AtomicFile
{
  public static void WriteAllText(string path, string text)
  {
    if (File.Exists(path) && File.ReadAllText(path) == text)
      return;
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.WriteAllText(temporary, text, new UTF8Encoding(false));
      Move(temporary, path);
    }
    finally
    {
      TryDelete(temporary);
    }
  }

  /// <summary>Copies <paramref name="source"/> over <paramref name="target"/> unless they already match.</summary>
  public static void Copy(string source, string target)
  {
    if (File.Exists(target) && SameContent(source, target))
      return;
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      File.Copy(source, temporary);
      Move(temporary, target);
    }
    finally
    {
      TryDelete(temporary);
    }
  }

  public static bool SameContent(string left, string right)
  {
    if (new FileInfo(left).Length != new FileInfo(right).Length)
      return false;
    using FileStream a = File.OpenRead(left);
    using FileStream b = File.OpenRead(right);
    byte[] bufferA = new byte[81920];
    byte[] bufferB = new byte[81920];
    while (true)
    {
      int read = a.Read(bufferA, 0, bufferA.Length);
      if (read == 0)
        return true;
      int filled = 0;
      while (filled < read)
      {
        int part = b.Read(bufferB, filled, read - filled);
        if (part == 0)
          return false;
        filled += part;
      }
      for (int i = 0; i < read; i++)
      {
        if (bufferA[i] != bufferB[i])
          return false;
      }
    }
  }

  private static void Move(string temporary, string path)
  {
    if (File.Exists(path))
      File.Replace(temporary, path, null);
    else
      File.Move(temporary, path);
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }
}
