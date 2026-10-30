using System;
using System.IO;
using System.Runtime.InteropServices;
using Element.Domain.DmNote;

namespace Element.Infrastructure.Game.DmNote;

/// <summary>Resolves exports from a loaded native viewer library.</summary>
internal interface INativeViewerLibrary
{
  IntPtr Symbol(string name);
}

internal sealed class MacNativeLibrary : INativeViewerLibrary
{
  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlopen(string path, int mode);

  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlsym(IntPtr handle, string name);

  // Objective-C classes and WebKit callbacks may outlive a view. Keep the image loaded.
  private static IntPtr _handle;

  private MacNativeLibrary() { }

  public static MacNativeLibrary Load(string path)
  {
    if (_handle == IntPtr.Zero)
    {
      if (!File.Exists(path))
        throw new NativeViewerHostException(
          NativeViewerStatus.ComponentMissing,
          "Native viewer library is missing: " + path
        );
      _handle = dlopen(path, 2);
      if (_handle == IntPtr.Zero)
        throw new NativeViewerHostException(
          NativeViewerStatus.ComponentMissing,
          "Native viewer library could not load: " + path
        );
    }
    return new MacNativeLibrary();
  }

  public IntPtr Symbol(string name) => dlsym(_handle, name);
}

internal sealed class WindowsNativeLibrary : INativeViewerLibrary
{
  private const int ProcedureNotFound = 127;

  [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
  private static extern IntPtr LoadLibraryW(string path);

  [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
  private static extern IntPtr GetProcAddress(IntPtr module, string name);

  // WebView2 callbacks and the host's timer window may run after a view is disposed.
  private static IntPtr _handle;

  private WindowsNativeLibrary() { }

  public static WindowsNativeLibrary Load(string path)
  {
    if (_handle == IntPtr.Zero)
    {
      if (!File.Exists(path))
        throw new NativeViewerHostException(
          NativeViewerStatus.ComponentMissing,
          "Native viewer library is missing: " + path
        );
      _handle = LoadLibraryW(path);
      if (_handle == IntPtr.Zero)
      {
        int error = Marshal.GetLastWin32Error();
        // The library imports Windows 10 APIs; older systems cannot resolve them.
        throw new NativeViewerHostException(
          error == ProcedureNotFound ? NativeViewerStatus.UnsupportedSystem : NativeViewerStatus.ComponentMissing,
          "Native viewer library could not load (error " + error + "): " + path
        );
      }
    }
    return new WindowsNativeLibrary();
  }

  public IntPtr Symbol(string name) => GetProcAddress(_handle, name);
}
