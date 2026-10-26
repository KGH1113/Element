using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImplResourcePack.Domain.DmNote;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

/// <summary>
/// The in-game viewer ABI (native/game-webview/GameWebView.h). macOS and Windows
/// ship different libraries behind the same exports; only loading differs.
/// </summary>
internal sealed class GameWebView : INativeViewerHost
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

  public GameWebView(INativeViewerLibrary library)
  {
    if (library == null)
      throw new ArgumentNullException(nameof(library));
    _setSurface = Symbol<SetSurface>(library, "irp_webview_set_surface");
    _setVisible = Symbol<SetVisible>(library, "irp_webview_set_visible");
    _setLayout = Symbol<SetLayout>(library, "irp_webview_set_layout");
    _setBounds = Symbol<SetBounds>(library, "irp_webview_set_bounds");
    _getReferenceSize = Symbol<GetReferenceSize>(library, "irp_webview_get_reference_size");
    _getGeometryRevision = Symbol<GetGeometryRevision>(library, "irp_webview_geometry_revision");
    _getBounds = Symbol<GetBounds>(library, "irp_webview_get_bounds");
    _getSelectionBounds = Symbol<GetBounds>(library, "irp_webview_get_selection_bounds");
    _getPointer = Symbol<GetPointer>(library, "irp_webview_get_pointer");
    _setEditing = Symbol<SetEditing>(library, "irp_webview_set_editing");
    _setSelected = Symbol<SetSelected>(library, "irp_webview_set_selected");
    _setHighlight = Symbol<SetHighlight>(library, "irp_webview_set_highlight");
    _setEditorExclusion = Symbol<SetEditorExclusion>(library, "irp_webview_set_editor_exclusion");
    _dispose = Symbol<DisposeViews>(library, "irp_webview_dispose");
    _lastError = Symbol<LastError>(library, "irp_webview_last_error");
    if (Symbol<Supported>(library, "irp_webview_supported")() == 0)
    {
      // The library explains why: an old macOS, or no WebView2 Runtime on Windows.
      string message = LastErrorText;
      throw new NativeViewerHostException(
        NativeViewerErrors.Classify(message, unknown: NativeViewerStatus.UnsupportedSystem),
        string.IsNullOrWhiteSpace(message) ? "Native viewers are not supported on this system." : message
      );
    }
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

  public bool TryGetFault(out NativeViewerStatus status, out string message)
  {
    message = _disposed ? null : LastErrorText;
    status = NativeViewerErrors.Classify(message);
    return !string.IsNullOrWhiteSpace(message);
  }

  private string LastErrorText => Marshal.PtrToStringAnsi(_lastError());

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
    string message = LastErrorText;
    return string.IsNullOrWhiteSpace(message)
      ? new NativeViewerHostException(NativeViewerStatus.WindowUnavailable, "Could not attach native viewers.")
      : new NativeViewerHostException(NativeViewerErrors.Classify(message), message);
  }

  private static T Symbol<T>(INativeViewerLibrary library, string name)
    where T : Delegate
  {
    IntPtr address = library.Symbol(name);
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
