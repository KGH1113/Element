using System;
using System.Collections.Generic;
using Element.Application.Gameplay;
using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Domain.Rendering;
using Element.Infrastructure.ElementNote.App;
using Element.Infrastructure.ElementNote.Ipc;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// ElementNote's key viewers drawn inside the game window: what the layout editor
/// places, what a play shows and what a render lays into its video. Puts the parts
/// together (the describe session and its IPC endpoint, the layout targets, the
/// native host) and applies what ElementNote reported once per frame.
/// </summary>
internal sealed class ElementNoteNativeViewers : IKeyViewerElements, IInGameKeyViewers, INativeViewerLayer, IDisposable
{
  private readonly bool _supported;
  private readonly NativeViewerSession _session;
  private readonly NativeViewerIpcEndpoint _endpoint;
  private readonly NativeViewerErrorLog _log;
  private readonly NativeViewerTargets _targets;
  private readonly NativeViewerHostLifetime _host;
  private readonly NativeViewerRenderPlacements _placements;
  private readonly ElementNoteTabOpener _tabs;
  private bool _disposed;
  private bool _hiddenInApp;
  private bool _connected;

  public Action EnabledChanged { get; set; }

  public ElementNoteNativeViewers(
    bool supported,
    Func<INativeViewerHost> createHost,
    Func<Color> highlight,
    Func<string, string> viewerLabel,
    Action<string, Exception> logError,
    Action<string> logWarning,
    IElementNoteAppFocus appFocus = null
  )
  {
    _supported = supported;
    _session = new NativeViewerSession(supported);
    if (createHost == null)
      throw new ArgumentNullException(nameof(createHost));
    _endpoint = new NativeViewerIpcEndpoint(_session);
    _log = new NativeViewerErrorLog(logError, logWarning);
    _targets = new NativeViewerTargets(
      viewerLabel ?? (kind => kind),
      OnEditingChanged,
      OnEnabledChanged,
      () => _session.Status,
      () => HiddenFromEditor
    );
    _host = new NativeViewerHostLifetime(createHost, highlight ?? (() => Color.white), _session, _targets, _log);
    _placements = new NativeViewerRenderPlacements(_targets, _host);
    _tabs = new ElementNoteTabOpener(_session, _endpoint, appFocus, _log);
  }

  /// <summary>What Element's IPC channel hands ElementNote's events to.</summary>
  public IElementIpcEndpoint Endpoint => _endpoint;

  IReadOnlyList<IOverlayLayoutTarget> IKeyViewerElements.Targets => _targets.All;

  public IReadOnlyCollection<string> DefaultIds => _targets.DefaultIds;

  public KeyViewerAvailability Availability =>
    !_supported ? KeyViewerAvailability.Unsupported
    : !_connected ? KeyViewerAvailability.AppClosed
    : _hiddenInApp ? KeyViewerAvailability.HiddenInApp
    : KeyViewerAvailability.Ready;

  public bool Supported => _supported;

  public bool HasEnabledViewers => !_hiddenInApp && _targets.AnyEnabled;

  /// <summary>ElementNote's "Show inside ADOFAI" is off.</summary>
  public bool HiddenInApp => _hiddenInApp;

  public NativeViewerStatus Status => _session.Status;

  // The editor shows key viewers only while ElementNote is connected and shows
  // them in the game. Unlike HiddenInApp, a closed app keeps the session waiting.
  private bool HiddenFromEditor => !_connected || _hiddenInApp;

  /// <summary>
  /// Follows <paramref name="layouts"/>, such as a profile's: its viewers come,
  /// and viewers it lacks go instead of being added to it.
  /// </summary>
  /// <param name="legacyEnabled">
  /// At startup only: whether the old single hand and foot viewers were on,
  /// for settings from before each viewer was its own element.
  /// </param>
  public void ConfigureLayouts(List<OverlayLayoutSettings> layouts, bool legacyEnabled = false) =>
    _targets.ConfigureLayouts(layouts, legacyEnabled);

  public IOverlayLayoutTarget Add(string templateId) => _targets.Add(templateId);

  public IOverlayLayoutTarget Switch(IOverlayLayoutTarget target, string templateId) =>
    _targets.Switch(target, templateId);

  // Unity main thread, from the editor's button: the game is in front.
  public KeyViewerEditResult OpenInApp(IOverlayLayoutTarget target) =>
    target is NativeViewerLayoutTarget viewer ? _tabs.Open(viewer.Kind) : KeyViewerEditResult.Failed;

  /// <summary>
  /// The key viewers a replay render shows, placed in a video of the given size:
  /// the ones on screen in the game. Empty while ElementNote is closed or hides
  /// them. Unity main thread.
  /// </summary>
  public IReadOnlyList<RenderViewerPlacement> RenderPlacements(int videoWidth, int videoHeight)
  {
    if (!_supported || !_connected || _hiddenInApp)
      return new List<RenderViewerPlacement>();
    return _placements.For(videoWidth, videoHeight);
  }

  /// <summary>The samples the editor shows while a viewer has no live page, in the settings font.</summary>
  public void ConfigurePreview(IKeyViewerSamples samples) => _targets.ConfigurePreview(samples);

  // Unity main thread only. Settings temporarily cover the game while editing
  // uses native chrome because AppKit children are above the Unity framebuffer.
  public void SetVisible(bool visible) => _host.SetVisible(visible);

  /// <summary>The editor's panels in screen pixels from the top left, with their corner radii.</summary>
  public void SetEditorExclusions(IReadOnlyList<LayoutExclusion> screenTopLeftPixels) =>
    _host.ExcludeEditor(screenTopLeftPixels);

  public void SetActive(bool active)
  {
    if (!_disposed)
      _session.SetActive(active, DateTime.UtcNow);
    if (!active)
      _host.ForgetSurfaces();
  }

  // Called on Unity's main thread; IPC callbacks never access AppKit.
  public void Tick()
  {
    if (_disposed)
      return;
    _connected = _session.Connected;
    bool hiddenInApp = _session.HiddenInApp;
    if (hiddenInApp != _hiddenInApp)
    {
      // Hiding ends the session like removing the last viewer; showing again
      // starts a fresh one through the same path.
      _hiddenInApp = hiddenInApp;
      OnEnabledChanged();
    }
    NativeViewerWork work = _session.TakeWork();
    if (work.ResetHost)
      _host.Reset();
    if (work.HideHost)
      _host.Suspend();
    if (work.Surfaces != null)
      _host.Attach(work.Surfaces);
    _targets.TickPreview();
    _host.Tick();
    Exception error = _endpoint.RequestDescriptorIfDue();
    if (error != null)
      _log.Error("Request native ElementNote viewers", error);
  }

  private void OnEditingChanged(bool editing) => _host.Edit(editing);

  private void OnEnabledChanged()
  {
    // This callback runs on Unity's thread for add/delete and history restore.
    // Removing the last viewer ends the session, descriptor polling, and WebKit.
    EnabledChanged?.Invoke();
    if (!HasEnabledViewers)
    {
      SetActive(false);
      _host.Close();
      return;
    }
    _host.ApplyViewers();
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _session.Close();
    _endpoint.Attach(null);
    _host.Close();
    _targets.Dispose();
  }
}
