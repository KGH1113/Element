using System;
using System.IO;
using System.Runtime.InteropServices;
using ImplResourcePack.Domain.DmNote;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

internal sealed class MacGameWebView : IDisposable
{
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int Supported();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SetSurface(string kind, string url, int width, int height);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetVisible(int visible);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void DisposeViews();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate IntPtr LastError();

  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlopen(string path, int mode);

  [DllImport("/usr/lib/libSystem.B.dylib")]
  private static extern IntPtr dlsym(IntPtr handle, string name);

  // Objective-C classes and WebKit callbacks may outlive a view. Keep the image loaded.
  private static IntPtr _library;
  private readonly SetSurface _setSurface;
  private readonly SetVisible _setVisible;
  private readonly DisposeViews _dispose;
  private readonly LastError _lastError;
  private bool _disposed;

  public MacGameWebView(string modPath)
  {
    if (_library == IntPtr.Zero)
    {
      string path = Path.Combine(modPath, "native", "libImplGameWebView.dylib");
      if (!File.Exists(path))
        throw new InvalidOperationException("Native viewer library is missing. Reinstall the mod and restart ADOFAI.");
      _library = dlopen(path, 2);
      if (_library == IntPtr.Zero)
        throw new InvalidOperationException(
          "Native viewer library could not load. Reinstall the mod and restart ADOFAI."
        );
    }
    _setSurface = Symbol<SetSurface>("irp_webview_set_surface");
    _setVisible = Symbol<SetVisible>("irp_webview_set_visible");
    _dispose = Symbol<DisposeViews>("irp_webview_dispose");
    _lastError = Symbol<LastError>("irp_webview_last_error");
    if (Symbol<Supported>("irp_webview_supported")() == 0)
      throw new InvalidOperationException("Native viewers require macOS 11 or newer.");
  }

  public void Apply(NativeViewerSurface[] surfaces)
  {
    bool hand = false;
    bool foot = false;
    foreach (NativeViewerSurface surface in surfaces)
    {
      if (_setSurface(surface.ViewerKind, surface.Url, surface.Width, surface.Height) == 0)
        throw new InvalidOperationException(Error());
      hand |= surface.ViewerKind == "hand";
      foot |= surface.ViewerKind == "foot";
    }
    if (!hand)
      _setSurface("hand", null, 0, 0);
    if (!foot)
      _setSurface("foot", null, 0, 0);
    _setVisible(1);
  }

  public void Hide() => _setVisible(0);

  public string LoadError => Marshal.PtrToStringAnsi(_lastError());

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _dispose();
  }

  private string Error()
  {
    string message = LoadError;
    return string.IsNullOrWhiteSpace(message)
      ? "Could not attach native viewers. Switch to windowed mode and try again."
      : message;
  }

  private static T Symbol<T>(string name)
    where T : Delegate
  {
    IntPtr address = dlsym(_library, name);
    if (address == IntPtr.Zero)
      throw new MissingMethodException("Native viewer library is incompatible. Reinstall the mod and restart ADOFAI.");
    return Marshal.GetDelegateForFunctionPointer<T>(address);
  }
}
