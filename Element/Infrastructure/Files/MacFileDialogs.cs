using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Element.Application.Files;

namespace Element.Infrastructure.Files;

/// <summary>
/// NSOpenPanel and NSSavePanel through native/libElementFileDialog.dylib. The
/// library loads on first use, so players who never open a file window never
/// load it.
/// </summary>
internal sealed class MacFileDialogs : IFileDialogs
{
  public const string Library = "libElementFileDialog.dylib";
  private const int PathCapacity = 4096;

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int OpenFile(byte[] title, byte[] extension, byte[] output, int capacity);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SaveFile(byte[] title, byte[] fileName, byte[] extension, byte[] output, int capacity);

  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlopen(string path, int mode);

  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlsym(IntPtr handle, string name);

  private readonly string _path;
  private OpenFile _open;
  private SaveFile _save;

  public MacFileDialogs(string runtimePath)
  {
    _path = Path.Combine(runtimePath, "native", Library);
  }

  public FileDialogResult Open(FileDialogRequest request)
  {
    Load();
    byte[] output = new byte[PathCapacity];
    // The native side takes several extensions as one comma-separated list.
    string extensions = string.Join(",", request.OpenExtensions());
    return Result(_open(Utf8(request.Title), Utf8(extensions), output, output.Length), output);
  }

  public FileDialogResult Save(FileDialogRequest request)
  {
    Load();
    byte[] output = new byte[PathCapacity];
    return Result(
      _save(Utf8(request.Title), Utf8(request.FileName), Utf8(request.Extension), output, output.Length),
      output
    );
  }

  private void Load()
  {
    if (_open != null)
      return;
    if (!File.Exists(_path))
      throw new FileNotFoundException("The file window library is missing.", _path);
    // RTLD_NOW with an absolute path: never search for another library.
    IntPtr handle = dlopen(_path, 2);
    if (handle == IntPtr.Zero)
      throw new InvalidOperationException("Cannot load " + _path);
    _save = Symbol<SaveFile>(handle, "element_file_dialog_save");
    _open = Symbol<OpenFile>(handle, "element_file_dialog_open");
  }

  private static T Symbol<T>(IntPtr handle, string name)
    where T : Delegate
  {
    IntPtr address = dlsym(handle, name);
    if (address == IntPtr.Zero)
      throw new EntryPointNotFoundException(name);
    return Marshal.GetDelegateForFunctionPointer<T>(address);
  }

  private static FileDialogResult Result(int status, byte[] output)
  {
    switch (status)
    {
      case 1:
        int length = Array.IndexOf(output, (byte)0);
        return FileDialogResult.Chosen(Encoding.UTF8.GetString(output, 0, length < 0 ? output.Length : length));
      case 0:
        return FileDialogResult.Cancelled;
      case -2:
        return FileDialogResult.Unavailable(new PathTooLongException("The chosen path is too long."));
      default:
        return FileDialogResult.Unavailable(
          new InvalidOperationException("The file window must open on the main thread (status " + status + ").")
        );
    }
  }

  private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes((text ?? "") + "\0");
}
