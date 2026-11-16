using System;
using System.IO;

namespace Element.Tests.Support;

/// <summary>
/// A new folder in the system's temporary folder, deleted with everything in
/// it when disposed. With <c>inside</c>, <see cref="Path"/> is a folder of that
/// name in it, for code that expects a folder with a certain name.
/// </summary>
internal sealed class TempFolder : IDisposable
{
  private readonly string _root;

  public TempFolder(string prefix, string inside = null)
  {
    _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
    Path = inside == null ? _root : System.IO.Path.Combine(_root, inside);
    Directory.CreateDirectory(Path);
  }

  public string Path { get; }

  public void Dispose()
  {
    try
    {
      Directory.Delete(_root, true);
    }
    catch (IOException) { }
  }
}
