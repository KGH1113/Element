using System;
using System.Collections.Generic;
using System.Linq;
using AdofaiIpc.Contracts;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Infrastructure.Game.DmNote;
using ImplResourcePack.Presentation.Layout;
using TMPro;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class DmNoteNativeIpcFeature : IDisposable
{
  private static readonly TimeSpan ErrorCheckInterval = TimeSpan.FromMilliseconds(500);

  private readonly NativeViewerSession _session;
  private readonly Func<INativeViewerHost> _createHost;
  private readonly Func<Color> _highlight;
  private readonly Action<string, Exception> _logError;
  private readonly Action<string> _logWarning;
  private IFeatureRegistration _ipc;
  private INativeViewerHost _host;
  private DateTime _nextErrorCheckAt;
  private NativeViewerSurface[] _knownSurfaces = Array.Empty<NativeViewerSurface>();
  private bool _disposed;
  private bool _suspended;
  private bool _visible = true;
  private bool _editing;
  private Rect _editorExclusion;
  private readonly List<NativeViewerLayoutTarget> _layoutTargets;
  private readonly NativeViewerLayoutTarget[] _galleryTargets;
  private List<OverlayLayoutSettings> _layouts;
  private TMP_FontAsset _font;
  private string _lastLoggedError;
  public Action EnabledChanged { get; set; }

  public DmNoteNativeIpcFeature(
    bool supported,
    Func<INativeViewerHost> createHost,
    Func<Color> highlight,
    Action<string, Exception> logError,
    Action<string> logWarning
  )
  {
    _session = new NativeViewerSession(supported);
    _createHost = createHost ?? throw new ArgumentNullException(nameof(createHost));
    _highlight = highlight ?? (() => Color.white);
    _logError = logError ?? ((_, _) => { });
    _logWarning = logWarning ?? (_ => { });
    _galleryTargets = new[] { CreateTarget("hand", null), CreateTarget("foot", null) };
    _layoutTargets = new List<NativeViewerLayoutTarget>(_galleryTargets);
  }

  public IReadOnlyList<IOverlayLayoutTarget> LayoutTargets => _layoutTargets;
  public IReadOnlyList<IOverlayLayoutTarget> GalleryTargets => _galleryTargets;
  public bool HasEnabledViewers => _layoutTargets.Any(target => target.Enabled);

  public void ConfigureLayouts(List<OverlayLayoutSettings> layouts, bool legacyEnabled = true)
  {
    if (layouts == null)
      throw new ArgumentNullException(nameof(layouts));
    _layouts = layouts;
    foreach (OverlayLayoutSettings settings in layouts.ToArray())
    {
      if (settings == null || _layoutTargets.Any(target => target.Id == settings.Id))
        continue;
      string kind =
        settings.TemplateId == "native-hand" ? "hand"
        : settings.TemplateId == "native-foot" ? "foot"
        : null;
      if (kind == null)
        continue;
      NativeViewerLayoutTarget target = CreateTarget(kind, settings.Id);
      _layoutTargets.Add(target);
    }
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.Configure(layouts, legacyEnabled);
  }

  public IOverlayLayoutTarget AddElement(string templateId)
  {
    string kind =
      templateId == "native-hand" ? "hand"
      : templateId == "native-foot" ? "foot"
      : null;
    if (kind == null || _layouts == null)
      return null;
    string id = templateId + ":" + Guid.NewGuid().ToString("N");
    NativeViewerLayoutTarget target = CreateTarget(kind, id);
    _layouts.Add(
      new OverlayLayoutSettings
      {
        Id = id,
        TemplateId = templateId,
        Enabled = true,
        Customized = true,
        CenterX = .5f,
        CenterY = .5f,
      }
    );
    _layoutTargets.Add(target);
    target.Configure(_layouts);
    target.SetPreview(_editing);
    OnEnabledChanged();
    return target;
  }

  private NativeViewerLayoutTarget CreateTarget(string kind, string id)
  {
    NativeViewerLayoutTarget target = new(
      kind,
      kind == "hand" ? "손 키뷰어" : "발 키뷰어",
      SetEditing,
      OnEnabledChanged,
      id,
      () => Status
    );
    target.ConfigurePreview(_font);
    target.Bind(_host);
    return target;
  }

  public void ConfigurePreview(TMP_FontAsset font)
  {
    _font = font;
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.ConfigurePreview(font);
  }

  private void OnEnabledChanged()
  {
    // This callback runs on Unity's thread for add/delete and history restore.
    // Removing the last viewer ends the session, descriptor polling, and WebKit.
    EnabledChanged?.Invoke();
    if (!HasEnabledViewers)
    {
      SetActive(false);
      _host?.Dispose();
      _host = null;
      BindLayoutTargets();
      return;
    }
    if (_host != null)
    {
      ApplyEnabledSurfaces();
      BindLayoutTargets();
    }
  }

  private void ApplyEnabledSurfaces()
  {
    _host.Apply(
      _layoutTargets
        .Where(target => target.Enabled)
        .SelectMany(target =>
          _knownSurfaces
            .Where(surface => surface.ViewerKind == target.Kind)
            .Select(surface => (target.NativeId, surface))
        )
        .ToArray()
    );
  }

  // Unity main thread only. Settings temporarily cover the game while editing
  // uses native chrome because AppKit children are above the Unity framebuffer.
  public void SetVisible(bool visible)
  {
    _visible = visible;
    _host?.Show(_visible && !_suspended);
  }

  public void SetEditorExclusion(Rect screenTopLeftPixels)
  {
    int width = Screen.width;
    int height = Screen.height;
    _editorExclusion =
      width > 0 && height > 0
        ? new Rect(
          screenTopLeftPixels.x / width,
          screenTopLeftPixels.y / height,
          screenTopLeftPixels.width / width,
          screenTopLeftPixels.height / height
        )
        : default;
    _host?.ExcludeEditor(_editorExclusion);
  }

  private void SetEditing(bool editing)
  {
    _editing = editing;
    _host?.Edit(editing);
  }

  private void BindLayoutTargets()
  {
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.Bind(_host);
  }

  public NativeViewerStatus Status => _session.Status;

  public void Attach(IFeatureRegistration ipc) => _ipc = ipc;

  public void SetActive(bool active)
  {
    if (!_disposed)
      _session.SetActive(active, DateTime.UtcNow);
    if (!active)
      _knownSurfaces = Array.Empty<NativeViewerSurface>();
  }

  // IPC thread: the session only records state; Tick applies it to AppKit.
  public void OnPeerEvent(PeerEvent message) => _session.OnPeerEvent(message, DateTime.UtcNow);

  public void OnPeerDisconnected(IClientConnection peer) => _session.OnPeerDisconnected(peer.PeerId);

  // Called on Unity's main thread; IPC callbacks never access AppKit.
  public void Tick()
  {
    if (_disposed)
      return;
    NativeViewerWork work = _session.TakeWork();
    if (work.ResetHost)
    {
      _suspended = false;
      _host?.Dispose();
      _host = null;
      BindLayoutTargets();
    }
    if (work.HideHost && _host != null)
    {
      // Keep the pages loaded while ImplDmNote renders a replay; the next
      // descriptor shows them again without a reload.
      _suspended = true;
      _host.Show(false);
    }
    if (work.Surfaces != null)
      AttachSurfaces(work.Surfaces);
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.TickPreview();
    if (_host != null)
    {
      _host.ApplyHighlight(_highlight());
      CheckPageErrors();
    }
    IFeatureRegistration ipc = _ipc;
    Exception error = _session.RequestIfDue(
      DateTime.UtcNow,
      ipc == null ? null : (peer, json) => ipc.SendCommandToPeer(peer, NativeViewerSession.DescribeCommand, json)
    );
    if (error != null)
      LogOnce("Request native ImplDmNote viewers", error);
  }

  private void AttachSurfaces(NativeViewerSurface[] surfaces)
  {
    try
    {
      bool unchanged = _host != null && NativeViewerSurface.SameSet(surfaces, _knownSurfaces);
      _knownSurfaces = surfaces;
      if (!unchanged)
      {
        _host ??= _createHost();
        ApplyEnabledSurfaces();
        _host.ExcludeEditor(_editorExclusion);
        _host.Edit(_editing);
        BindLayoutTargets();
      }
      _suspended = false;
      _host.Show(_visible);
      _session.Report(surfaces.Length == 0 ? NativeViewerStatus.NoViewerTab : NativeViewerStatus.Attached);
    }
    catch (Exception error)
    {
      _host?.Dispose();
      _host = null;
      BindLayoutTargets();
      _session.Report(error is NativeViewerHostException host ? host.Status : NativeViewerStatus.WindowUnavailable);
      LogOnce("Attach native ImplDmNote viewers", error);
    }
  }

  // Page errors matter only while the editor can show them; skip the native
  // call and string marshaling during gameplay.
  private void CheckPageErrors()
  {
    DateTime now = DateTime.UtcNow;
    if (!_editing || now < _nextErrorCheckAt)
      return;
    _nextErrorCheckAt = now + ErrorCheckInterval;
    string error = _host.LoadError;
    if (string.IsNullOrWhiteSpace(error))
      return;
    _session.Report(MacGameWebView.Classify(error));
    if (error != _lastLoggedError)
    {
      _lastLoggedError = error;
      _logWarning("[Native viewers] " + error);
    }
  }

  private void LogOnce(string context, Exception error)
  {
    string key = context + ": " + error.GetType().Name + ": " + error.Message;
    if (key == _lastLoggedError)
      return;
    _lastLoggedError = key;
    _logError(context, error);
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    _session.Close();
    _ipc = null;
    _host?.Dispose();
    _host = null;
    BindLayoutTargets();
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.Dispose();
  }
}
