using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ImplResourcePack.Domain.DmNote;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

internal sealed class MacGameWebView : INativeViewerHost
{
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int Supported();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SetSurface(string kind, string url, int width, int height);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetVisible(int visible);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SetLayout(string kind, int customized, double centerX, double centerY, double scale);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int SetBounds(string kind, double x, double y, double width, double height, double scale);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int GetReferenceSize(string kind, out double width, out double height);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate ulong GetGeometryRevision();

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int GetBounds(
    string kind,
    out double x,
    out double y,
    out double width,
    out double height,
    out double scale
  );

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate int GetPointer(out double x, out double y);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetEditing(int editing);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetSelected(string kind, int selected);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetHighlight(double red, double green, double blue);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  private delegate void SetEditorExclusion(double x, double y, double width, double height);

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
  private readonly SetLayout _setLayout;
  private readonly SetBounds _setBounds;
  private readonly GetReferenceSize _getReferenceSize;
  private readonly GetGeometryRevision _getGeometryRevision;
  private readonly GetBounds _getBounds;
  private readonly GetBounds _getSelectionBounds;
  private readonly GetPointer _getPointer;
  private readonly SetEditing _setEditing;
  private readonly SetSelected _setSelected;
  private readonly SetHighlight _setHighlight;
  private readonly SetEditorExclusion _setEditorExclusion;
  private readonly DisposeViews _dispose;
  private readonly LastError _lastError;
  private bool _disposed;
  private readonly HashSet<string> _kinds = new();
  private bool _visible = true;
  private bool _editing;
  private Rect _editorExclusion;
  private int _pointerFrame = -1;
  private bool _pointerAvailable;
  private Vector2 _pointer;
  private Color _highlight = new(float.NaN, 0, 0);
  private int _geometryFrame = -1;
  private ulong _geometryRevision;

  public ulong GeometryRevision
  {
    get
    {
      if (!_disposed && _geometryFrame != Time.frameCount)
      {
        _geometryFrame = Time.frameCount;
        _geometryRevision = _getGeometryRevision();
      }
      return _geometryRevision;
    }
  }

  public MacGameWebView(string modPath)
  {
    // libSystem and the WebKit host exist only on macOS; never reach dlopen elsewhere.
    if (
      UnityEngine.Application.platform != RuntimePlatform.OSXPlayer
      && UnityEngine.Application.platform != RuntimePlatform.OSXEditor
    )
      throw new NativeViewerHostException(
        NativeViewerStatus.UnsupportedPlatform,
        "Native viewers are available only on macOS."
      );
    if (_library == IntPtr.Zero)
    {
      string path = Path.Combine(modPath, "native", "libImplGameWebView.dylib");
      if (!File.Exists(path))
        throw new NativeViewerHostException(
          NativeViewerStatus.ComponentMissing,
          "Native viewer library is missing: " + path
        );
      _library = dlopen(path, 2);
      if (_library == IntPtr.Zero)
        throw new NativeViewerHostException(
          NativeViewerStatus.ComponentMissing,
          "Native viewer library could not load: " + path
        );
    }
    _setSurface = Symbol<SetSurface>("irp_webview_set_surface");
    _setVisible = Symbol<SetVisible>("irp_webview_set_visible");
    _setLayout = Symbol<SetLayout>("irp_webview_set_layout");
    _setBounds = Symbol<SetBounds>("irp_webview_set_bounds");
    _getReferenceSize = Symbol<GetReferenceSize>("irp_webview_get_reference_size");
    _getGeometryRevision = Symbol<GetGeometryRevision>("irp_webview_geometry_revision");
    _getBounds = Symbol<GetBounds>("irp_webview_get_bounds");
    _getSelectionBounds = Symbol<GetBounds>("irp_webview_get_selection_bounds");
    _getPointer = Symbol<GetPointer>("irp_webview_get_pointer");
    _setEditing = Symbol<SetEditing>("irp_webview_set_editing");
    _setSelected = Symbol<SetSelected>("irp_webview_set_selected");
    _setHighlight = Symbol<SetHighlight>("irp_webview_set_highlight");
    _setEditorExclusion = Symbol<SetEditorExclusion>("irp_webview_set_editor_exclusion");
    _dispose = Symbol<DisposeViews>("irp_webview_dispose");
    _lastError = Symbol<LastError>("irp_webview_last_error");
    if (Symbol<Supported>("irp_webview_supported")() == 0)
      throw new NativeViewerHostException(
        NativeViewerStatus.UnsupportedSystem,
        "Native viewers require macOS 11 or newer."
      );
  }

  public void Apply((string Id, NativeViewerSurface Surface)[] surfaces)
  {
    HashSet<string> next = new();
    foreach ((string id, NativeViewerSurface surface) in surfaces)
    {
      if (_setSurface(id, surface.Url, surface.Width, surface.Height) == 0)
        throw HostError();
      next.Add(id);
    }
    foreach (string kind in _kinds)
      if (!next.Contains(kind))
        _setSurface(kind, null, 0, 0);
    _kinds.Clear();
    _kinds.UnionWith(next);
    _geometryFrame = -1;
  }

  public void Hide() => Show(false);

  public void ApplyHighlight(Color accent)
  {
    if (_disposed || _highlight == accent)
      return;
    _highlight = accent;
    _setHighlight(accent.r, accent.g, accent.b);
  }

  public bool HasSurface(string kind) => !_disposed && _kinds.Contains(kind);

  public void Show(bool visible)
  {
    if (_disposed || _visible == visible)
      return;
    _visible = visible;
    _setVisible(visible ? 1 : 0);
  }

  public void Edit(bool editing)
  {
    if (_disposed || _editing == editing)
      return;
    _editing = editing;
    _setEditing(editing ? 1 : 0);
  }

  public void ExcludeEditor(Rect normalizedBounds)
  {
    if (_disposed || _editorExclusion == normalizedBounds)
      return;
    _editorExclusion = normalizedBounds;
    _setEditorExclusion(normalizedBounds.x, normalizedBounds.y, normalizedBounds.width, normalizedBounds.height);
  }

  public void Select(string kind, bool selected)
  {
    if (HasSurface(kind))
      _setSelected(kind, selected ? 1 : 0);
  }

  public void Layout(string kind, bool customized, float centerX, float centerY, float scale)
  {
    if (HasSurface(kind) && _setLayout(kind, customized ? 1 : 0, centerX, centerY, scale) == 0)
      throw HostError();
  }

  public void LayoutBounds(string kind, Rect normalizedBounds, float effectiveScale)
  {
    if (
      HasSurface(kind)
      && _setBounds(
        kind,
        normalizedBounds.x,
        normalizedBounds.y,
        normalizedBounds.width,
        normalizedBounds.height,
        effectiveScale
      ) == 0
    )
      throw HostError();
  }

  public bool TryGetReferenceSize(string kind, out Vector2 size)
  {
    size = default;
    if (!HasSurface(kind) || _getReferenceSize(kind, out double width, out double height) == 0)
      return false;
    size = new Vector2((float)width, (float)height);
    return width > 0 && height > 0;
  }

  public bool TryGetBounds(string kind, out Rect normalizedBounds, out float effectiveScale)
  {
    normalizedBounds = default;
    effectiveScale = 1f;
    if (
      !HasSurface(kind)
      || _getBounds(kind, out double x, out double y, out double width, out double height, out double scale) == 0
    )
      return false;
    normalizedBounds = new Rect((float)x, (float)y, (float)width, (float)height);
    effectiveScale = (float)scale;
    return width > 0 && height > 0;
  }

  public string LoadError => Marshal.PtrToStringAnsi(_lastError());

  public bool TryGetSelectionBounds(string kind, out Rect normalizedBounds)
  {
    normalizedBounds = default;
    if (
      !HasSurface(kind)
      || _getSelectionBounds(kind, out double x, out double y, out double width, out double height, out _) == 0
    )
      return false;
    normalizedBounds = new Rect((float)x, (float)y, (float)width, (float)height);
    return width > 0 && height > 0;
  }

  public bool TryGetPointer(out Vector2 normalized)
  {
    if (_pointerFrame != Time.frameCount)
    {
      _pointerFrame = Time.frameCount;
      double x = 0,
        y = 0;
      _pointerAvailable = !_disposed && _getPointer(out x, out y) != 0;
      if (_pointerAvailable)
        _pointer = new Vector2((float)x, (float)y);
    }
    normalized = _pointer;
    return _pointerAvailable && !_disposed;
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _kinds.Clear();
    _dispose();
  }

  private NativeViewerHostException HostError()
  {
    string message = LoadError;
    return string.IsNullOrWhiteSpace(message)
      ? new NativeViewerHostException(NativeViewerStatus.WindowUnavailable, "Could not attach native viewers.")
      : new NativeViewerHostException(Classify(message), message);
  }

  /// <summary>Maps the native host's diagnostic text to what the user can do about it.</summary>
  public static NativeViewerStatus Classify(string nativeError)
  {
    if (string.IsNullOrWhiteSpace(nativeError))
      return NativeViewerStatus.Attached;
    if (nativeError.Contains("renderer stopped"))
      return NativeViewerStatus.Reconnecting;
    if (nativeError.Contains("macOS 11"))
      return NativeViewerStatus.UnsupportedSystem;
    if (nativeError.Contains("game window") || nativeError.Contains("application is not ready"))
      return NativeViewerStatus.WindowUnavailable;
    return NativeViewerStatus.PageUnavailable;
  }

  private static T Symbol<T>(string name)
    where T : Delegate
  {
    IntPtr address = dlsym(_library, name);
    if (address == IntPtr.Zero)
      throw new NativeViewerHostException(
        NativeViewerStatus.ComponentMissing,
        "Native viewer library is incompatible: missing " + name
      );
    return Marshal.GetDelegateForFunctionPointer<T>(address);
  }
}

internal sealed class NativeViewerHostException : Exception
{
  public NativeViewerStatus Status { get; }

  public NativeViewerHostException(NativeViewerStatus status, string message)
    : base(message) => Status = status;
}
