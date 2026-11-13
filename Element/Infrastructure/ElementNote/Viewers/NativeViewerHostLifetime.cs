using System;
using System.Collections.Generic;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// The native host that draws ElementNote's pages in the game window: made when
/// ElementNote describes its viewers, shown and hidden for the settings window and
/// replay renders, and let go when the session resets or no viewer is left. Unity
/// main thread only; IPC callbacks never reach AppKit or WebView2.
/// </summary>
internal sealed class NativeViewerHostLifetime
{
  private static readonly TimeSpan ErrorCheckInterval = TimeSpan.FromMilliseconds(500);

  private readonly Func<INativeViewerHost> _createHost;
  private readonly Func<Color> _highlight;
  private readonly NativeViewerSession _session;
  private readonly NativeViewerTargets _targets;
  private readonly NativeViewerErrorLog _log;
  private INativeViewerHost _host;
  private NativeViewerSurface[] _knownSurfaces = Array.Empty<NativeViewerSurface>();

  // Filled again each frame the layout editor is open, without a new list.
  private readonly List<LayoutExclusion> _editorExclusions = new();
  private DateTime _nextErrorCheckAt;
  private bool _suspended;
  private bool _visible = true;

  public NativeViewerHostLifetime(
    Func<INativeViewerHost> createHost,
    Func<Color> highlight,
    NativeViewerSession session,
    NativeViewerTargets targets,
    NativeViewerErrorLog log
  )
  {
    _createHost = createHost ?? throw new ArgumentNullException(nameof(createHost));
    _highlight = highlight;
    _session = session;
    _targets = targets;
    _log = log;
  }

  /// <summary>The last page ElementNote described for <paramref name="viewerKind"/>; null when none.</summary>
  public NativeViewerSurface KnownSurface(string viewerKind)
  {
    foreach (NativeViewerSurface surface in _knownSurfaces)
      if (surface.ViewerKind == viewerKind)
        return surface;
    return null;
  }

  // Settings temporarily cover the game while editing uses native chrome,
  // because AppKit children are above the Unity framebuffer.
  public void SetVisible(bool visible)
  {
    _visible = visible;
    _host?.Show(_visible && !_suspended);
  }

  /// <summary>The editor's panels in screen pixels from the top left, with their corner radii.</summary>
  public void ExcludeEditor(IReadOnlyList<LayoutExclusion> screenTopLeftPixels)
  {
    int width = Screen.width;
    int height = Screen.height;
    _editorExclusions.Clear();
    if (width > 0 && height > 0)
      for (int i = 0; i < screenTopLeftPixels.Count; i++)
      {
        LayoutExclusion exclusion = screenTopLeftPixels[i];
        _editorExclusions.Add(
          new LayoutExclusion(
            new LayoutBounds(
              exclusion.Bounds.X / width,
              exclusion.Bounds.Y / height,
              exclusion.Bounds.Width / width,
              exclusion.Bounds.Height / height
            ),
            exclusion.Radius / height
          )
        );
      }
    _host?.ExcludeEditor(_editorExclusions);
  }

  public void Edit(bool editing) => _host?.Edit(editing);

  /// <summary>The session ended: its pages are gone, so a new one starts from fresh descriptors.</summary>
  public void ForgetSurfaces() => _knownSurfaces = Array.Empty<NativeViewerSurface>();

  /// <summary>The enabled viewers changed: the host shows their pages.</summary>
  public void ApplyViewers()
  {
    if (_host == null)
      return;
    ApplyEnabledSurfaces();
    _targets.Bind(_host);
  }

  /// <summary>Lets the host go: its pages, their polling and WebKit end.</summary>
  public void Close()
  {
    _host?.Dispose();
    _host = null;
    _targets.Bind(_host);
  }

  /// <summary>The session starts over; the next descriptor makes a new host.</summary>
  public void Reset()
  {
    _suspended = false;
    Close();
  }

  /// <summary>
  /// Keeps the pages loaded while ElementNote renders a replay; the next
  /// descriptor shows them again without a reload.
  /// </summary>
  public void Suspend()
  {
    if (_host == null)
      return;
    _suspended = true;
    _host.Show(false);
  }

  /// <summary>ElementNote described its viewers: show their pages, making the host if there is none.</summary>
  public void Attach(NativeViewerSurface[] surfaces)
  {
    try
    {
      bool unchanged = _host != null && NativeViewerSurface.SameSet(surfaces, _knownSurfaces);
      _knownSurfaces = surfaces;
      if (!unchanged)
      {
        _host ??= _createHost();
        _host.FitGame(Screen.width, Screen.height);
        ApplyEnabledSurfaces();
        _host.ExcludeEditor(_editorExclusions);
        _host.Edit(_targets.Editing);
        _targets.Bind(_host);
      }
      else if (PageNeedsDescriptor())
      {
        // The host dropped a stopped or failed page and waits for its source
        // again; an identical descriptor is that retry (every poll, at most 30 s).
        ApplyEnabledSurfaces();
      }
      _suspended = false;
      _host.Show(_visible);
      _session.Report(surfaces.Length == 0 ? NativeViewerStatus.NoViewerTab : NativeViewerStatus.Attached);
    }
    catch (Exception error)
    {
      Close();
      _session.Report(error is NativeViewerHostException host ? host.Status : NativeViewerStatus.WindowUnavailable);
      _log.Error("Attach native ElementNote viewers", error);
    }
  }

  /// <summary>Every frame: the accent color, and page errors while the editor shows them.</summary>
  public void Tick()
  {
    if (_host == null)
      return;
    // A resolution change in the game's options moves where the game is drawn.
    _host.FitGame(Screen.width, Screen.height);
    _host.ApplyHighlight(_highlight());
    CheckPageErrors();
  }

  private void ApplyEnabledSurfaces() => _host.Apply(_targets.EnabledSurfaces(_knownSurfaces));

  // Runs once per descriptor reply, not per frame.
  private bool PageNeedsDescriptor() =>
    _host.TryGetFault(out NativeViewerStatus status, out _) && NativeViewerErrors.NeedsDescriptor(status);

  // Page errors matter only while the editor can show them; skip the native
  // call and string marshaling during gameplay.
  private void CheckPageErrors()
  {
    DateTime now = DateTime.UtcNow;
    if (!_targets.Editing || now < _nextErrorCheckAt)
      return;
    _nextErrorCheckAt = now + ErrorCheckInterval;
    if (!_host.TryGetFault(out NativeViewerStatus status, out string error))
      return;
    _session.Report(status);
    _log.PageFault(error);
  }
}
