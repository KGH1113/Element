using System;
using System.IO;
using System.Runtime.InteropServices;
using Element.Domain.KeyViewer;

namespace Element.Infrastructure.ElementNote.Viewers;

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
