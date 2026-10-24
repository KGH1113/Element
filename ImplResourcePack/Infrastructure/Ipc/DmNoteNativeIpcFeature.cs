using System;
using System.Collections.Generic;
using System.Linq;
using AdofaiIpc.Contracts;
using ImplResourcePack.Configuration;
using ImplResourcePack.Domain.DmNote;
using ImplResourcePack.Infrastructure.Game.DmNote;
using ImplResourcePack.Presentation.Layout;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Ipc;

internal sealed class DmNoteNativeIpcFeature : IDisposable
{
  private readonly object _gate = new();
  private IFeatureRegistration _ipc;
  private MacGameWebView _host;
  private string _peerId;
  private string _sessionId;
  private string _pendingId;
  private DateTime _pendingAt;
  private DateTime _nextRequestAt;
  private DateTime _nextErrorCheckAt;
  private NativeViewerSurface[] _surfaces;
  private NativeViewerSurface[] _knownSurfaces = Array.Empty<NativeViewerSurface>();
  private bool _active;
  private bool _disposed;
  private bool _reset;
  private bool _visible = true;
  private bool _editing;
  private Rect _editorExclusion;
  private readonly List<NativeViewerLayoutTarget> _layoutTargets;
  private readonly NativeViewerLayoutTarget[] _galleryTargets;
  private List<OverlayLayoutSettings> _layouts;
  private TMP_FontAsset _font;
  private string _status = "Enable OBS mode in ImplDmNote, then start a level or show the preview.";
  public Action EnabledChanged { get; set; }

  public DmNoteNativeIpcFeature()
  {
    _galleryTargets = new[]
    {
      new NativeViewerLayoutTarget("hand", "손 키뷰어", SetEditing, OnEnabledChanged),
      new NativeViewerLayoutTarget("foot", "발 키뷰어", SetEditing, OnEnabledChanged),
    };
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
      id
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
    _host?.Show(visible);
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

  private sealed class Reply
  {
    public string SessionId { get; set; }
    public NativeViewerSurface[] Surfaces { get; set; }
  }

  public string Status
  {
    get
    {
      lock (_gate)
        return _status;
    }
  }

  public void Attach(IFeatureRegistration ipc) => _ipc = ipc;

  public void SetActive(bool active)
  {
    lock (_gate)
    {
      if (_disposed || _active == active)
        return;
      _active = active;
      _sessionId = active ? Guid.NewGuid().ToString("N") : null;
      _pendingId = null;
      _surfaces = null;
      if (!active)
        _knownSurfaces = Array.Empty<NativeViewerSurface>();
      _nextRequestAt = DateTime.UtcNow;
      _reset = true;
      if (active)
        _status =
          _peerId == null
            ? "Waiting for ImplDmNote. Open the updated app and enable OBS mode."
            : "Connecting to OBS viewers...";
    }
  }

  public void OnPeerEvent(PeerEvent message)
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      if (message.Name == "mirror.available")
      {
        try
        {
          if ((int?)JObject.Parse(message.Json)?["nativeProtocolVersion"] == 1 && _peerId == null)
          {
            _peerId = message.PeerId;
            _nextRequestAt = DateTime.UtcNow;
          }
        }
        catch (JsonException)
        {
          // A malformed capability advertisement cannot select the native provider.
        }
        return;
      }
      if (!_active || message.PeerId != _peerId || message.CorrelationId != _pendingId)
        return;
      if (message.Name != "mirror.native.ready" && message.Name != "mirror.failed")
        return;
      _pendingId = null;
      _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
      if (message.Name == "mirror.failed")
      {
        _reset = true;
        string code = null;
        try
        {
          code = (string)JObject.Parse(message.Json)?["code"];
        }
        catch (JsonException) { }
        _status = code switch
        {
          "replay-render-active" =>
            "Replay rendering is using ImplDmNote. Native viewers will reconnect when it finishes.",
          "obs-not-ready" => "The OBS viewer service is starting. Native viewers will reconnect automatically.",
          "mirror-busy" => "ImplDmNote is finishing another viewer request. Retrying shortly.",
          _ =>
            "OBS viewers are unavailable. Enable OBS mode in ImplDmNote; native viewers will reconnect automatically.",
        };
        return;
      }
      try
      {
        Reply reply = JsonConvert.DeserializeObject<Reply>(message.Json);
        if (reply?.SessionId != _sessionId)
          return;
        if (
          reply.Surfaces == null
          || reply.Surfaces.Length > 2
          || reply.Surfaces.Any(surface => surface == null || !surface.IsValid())
          || reply.Surfaces.Select(surface => surface.ViewerKind).Distinct().Count() != reply.Surfaces.Length
        )
          throw new JsonSerializationException();
        _surfaces = reply.Surfaces;
      }
      catch (JsonException)
      {
        _reset = true;
        _status = "The app sent an incompatible native viewer description. Update both the app and mod.";
      }
    }
  }

  public void OnPeerDisconnected(IClientConnection peer)
  {
    lock (_gate)
    {
      if (peer.PeerId != _peerId)
        return;
      _peerId = null;
      _pendingId = null;
      _surfaces = null;
      _reset = true;
      _status = "ImplDmNote disconnected. Reopen the app and enable OBS mode.";
    }
  }

  // Called on Unity's main thread; IPC callbacks never access AppKit.
  public void Tick()
  {
    NativeViewerSurface[] surfaces;
    bool reset;
    lock (_gate)
    {
      if (_disposed)
        return;
      reset = _reset;
      _reset = false;
      surfaces = _surfaces;
      _surfaces = null;
    }
    if (reset)
    {
      _host?.Dispose();
      _host = null;
      BindLayoutTargets();
    }
    if (surfaces != null)
    {
      try
      {
        _host ??= new MacGameWebView(Main.Instance.Path);
        _knownSurfaces = surfaces;
        ApplyEnabledSurfaces();
        _host.Show(_visible);
        _host.ExcludeEditor(_editorExclusion);
        _host.Edit(_editing);
        BindLayoutTargets();
        lock (_gate)
          _status =
            surfaces.Length == 0
              ? "No viewers selected. Open a hand or foot viewer before enabling OBS mode."
              : "Native viewers attached. Check the Discord / OBS capture preview.";
      }
      catch (Exception error)
      {
        _host?.Dispose();
        _host = null;
        BindLayoutTargets();
        lock (_gate)
          _status =
            error is InvalidOperationException
              ? error.Message
              : "Native viewers could not open. Reinstall the mod and restart ADOFAI.";
      }
    }
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.TickPreview();
    _host?.RefreshTheme();
    if (_host != null && DateTime.UtcNow >= _nextErrorCheckAt)
    {
      _nextErrorCheckAt = DateTime.UtcNow.AddMilliseconds(500);
      string error = _host.LoadError;
      if (!string.IsNullOrWhiteSpace(error))
      {
        lock (_gate)
          _status = error;
      }
    }
    lock (_gate)
    {
      if (!_active || _peerId == null || _ipc == null || DateTime.UtcNow < _nextRequestAt)
        return;
      if (_pendingId != null && DateTime.UtcNow - _pendingAt < TimeSpan.FromSeconds(10))
        return;
      try
      {
        _pendingId = _ipc.SendCommandToPeer(
          _peerId,
          "mirror.native.describe",
          JsonConvert.SerializeObject(new { sessionId = _sessionId })
        );
        _pendingAt = DateTime.UtcNow;
        _nextRequestAt = _pendingAt.AddSeconds(2);
      }
      catch
      {
        _nextRequestAt = DateTime.UtcNow.AddSeconds(2);
        _status = "Could not reach ImplDmNote. Check that the updated app is connected to the game.";
      }
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      _active = false;
      _ipc = null;
      _surfaces = null;
    }
    _host?.Dispose();
    _host = null;
    BindLayoutTargets();
    foreach (NativeViewerLayoutTarget target in _layoutTargets)
      target.Dispose();
  }
}
