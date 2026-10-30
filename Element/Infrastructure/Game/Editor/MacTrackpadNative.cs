using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Element.Infrastructure.Game.Editor;

internal sealed class MacTrackpadNative : IDisposable
{
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Start();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Stop();
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Enable(int enabled);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Drain([Out] TrackpadEvent[] output, int capacity);
  [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlopen(string path, int mode);
  [DllImport("/usr/lib/libSystem.B.dylib")] private static extern IntPtr dlsym(IntPtr handle, string name);
  [DllImport("/usr/lib/libSystem.B.dylib")] private static extern int dlclose(IntPtr handle);
  private IntPtr _library;
  private Stop _stop;
  private Enable _enable;
  private Drain _drain;

  public MacTrackpadNative(string modPath)
  {
    string path = Path.Combine(modPath, "libElementTrackpad.dylib");
    if (!File.Exists(path)) throw new FileNotFoundException("Trackpad native library is missing", path);
    _library = dlopen(path, 2); // RTLD_NOW, absolute path: do not search arbitrary libraries.
    if (_library == IntPtr.Zero) throw new InvalidOperationException("Cannot load " + path);
    try
    {
      _stop = Symbol<Stop>("irp_trackpad_stop");
      _enable = Symbol<Enable>("irp_trackpad_enable");
      _drain = Symbol<Drain>("irp_trackpad_drain");
      if (Symbol<Start>("irp_trackpad_start")() == 0)
        throw new InvalidOperationException("Cannot register macOS trackpad event monitor on this thread");
    }
    catch { Dispose(); throw; }
  }

  private T Symbol<T>(string name) where T : Delegate
  {
    IntPtr address = dlsym(_library, name);
    if (address == IntPtr.Zero) throw new MissingMethodException(name);
    return Marshal.GetDelegateForFunctionPointer<T>(address);
  }
  public void SetEnabled(bool enabled) => _enable(enabled ? 1 : 0);
  public int Read(TrackpadEvent[] output) => _drain(output, output.Length);
  public void Dispose()
  {
    if (_library == IntPtr.Zero) return;
    _stop?.Invoke();
    dlclose(_library);
    _library = IntPtr.Zero;
  }
}
