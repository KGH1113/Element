using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;

namespace Element.Infrastructure.Game.DmNote;

// Geometry stays local to the game. Moving a viewer never sends IPC messages or
// reloads its OBS page; WebKit preserves the original CSS viewport with pageZoom.
internal sealed class NativeViewerLayoutTarget
  : IOverlayLayoutTarget,
    IExternalLayoutOutline,
    IOverlayLayoutPointer,
    IOverlayGalleryPreview,
    IDisposable
{
  private readonly string _kind;
  private readonly string _label;
  private readonly Action<bool> _setPreview;
  private readonly Action _enabledChanged;
  private readonly Func<NativeViewerStatus> _status;
  private readonly Func<bool> _hidden;
  private NativeViewerMock _mock;
  private OverlayLayoutSettings _settings;
  private INativeViewerLayoutHost _host;
  private Rect _bounds;
  private float _effectiveScale = 1f;
  private int _screenWidth;
  private int _screenHeight;
  private float _screenScale = 1;
  // Unity pixels per reference unit for sizes; 0 until the host reports its
  // desktop units. With them, 100% is the size the separate ElementNote window
  // shows (one CSS pixel per point or DIP); without, the 1920x1080 scale.
  private float _sizeUnit;
  private ulong _geometryRevision;
  private Vector2 _referenceSize;
  private OverlayLayoutSettings _growthLayout;
  private (bool, int, Anchor9, float, float, float, float, float) _growthKey;
  private bool _staged;
  private Rect _stagedBounds;
  private Rect _stageStartBounds;
  private bool _applying;
  private bool _selected;
  private bool _preview;
  private bool _lastEnabled = true;
  private TMP_FontAsset _font;
  private GameObject _previewCanvas;
  private RectTransform _previewRoot;
  private Rect _previewBounds = new(float.NaN, 0, 0, 0);

  public NativeViewerLayoutTarget(
    string kind,
    string label,
    Action<bool> setPreview,
    Action enabledChanged,
    string id = null,
    Func<NativeViewerStatus> status = null,
    Func<bool> hidden = null
  )
  {
    _status = status;
    _hidden = hidden;
    _kind = kind;
    Id = id ?? "dmnote-" + kind;
    TemplateId = "native-" + kind;
    NativeId = kind + ":" + Id;
    _label = label;
    _setPreview = setPreview;
    _enabledChanged = enabledChanged;
    _settings = new OverlayLayoutSettings { Id = Id };
  }

  public string Id { get; }
  public string TemplateId { get; }
  public string Kind => _kind;
  public string NativeId { get; }
  public string Label => SettingsLocale.L(_label, _kind == "hand" ? "Hand keyviewer" : "Foot keyviewer");
  public bool Enabled => _settings.Enabled;

  // While ElementNote is closed or hides its viewers, the editor neither draws nor selects them.
  private bool Hidden => _hidden?.Invoke() == true;
  public bool Available => !Hidden && (OwnsSelectionOutline || _preview);
  public bool OwnsSelectionOutline => _host?.HasSurface(NativeId) == true;
  public Rect SelectionBounds
  {
    get
    {
      Rect bounds = Bounds;
      if (_host != null && _host.TryGetSelectionBounds(NativeId, out Rect normalized))
        return new Rect(
          normalized.x * Screen.width,
          normalized.y * Screen.height,
          normalized.width * Screen.width,
          normalized.height * Screen.height
        );
      // Mock keys occupy the lower portion of the full note viewport.
      return new Rect(bounds.x, bounds.y + bounds.height * .55f, bounds.width, bounds.height * .45f);
    }
  }
  public float EffectiveScale
  {
    get
    {
      // Refresh the viewport scale after a game-window size change before the editor
      // calculates its drag limits from the visible size.
      _ = Bounds;
      return _effectiveScale;
    }
  }
  public Rect Bounds
  {
    get
    {
      // WebKit can finish reading content padding after navigation without a
      // screen-size change. Hit targets must follow those actual native bounds.
      EnsureLayoutCurrent();
      return _bounds;
    }
  }

  public void Configure(List<OverlayLayoutSettings> layouts, bool legacyEnabled = true)
  {
    _settings = layouts.Find(layout => layout != null && layout.Id == Id);
    if (_settings == null)
    {
      _settings = new OverlayLayoutSettings
      {
        Id = Id,
        TemplateId = TemplateId,
        Enabled = legacyEnabled,
      };
      layouts.Add(_settings);
    }
    else if (string.IsNullOrEmpty(_settings.TemplateId))
    {
      // Existing layout entries predate per-element presence. Migrate the old
      // native viewer toggle once, then keep all history/add/delete state local.
      _settings.TemplateId = TemplateId;
      _settings.Enabled = legacyEnabled;
    }
    _staged = false;
    Apply();
  }

  public void ConfigurePreview(TMP_FontAsset font) => _font = font;

  public GameObject CreateGalleryPreview(Transform parent)
  {
    RectTransform root = NativeViewerMock.Create(_kind, parent, _font, false);
    root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
    root.anchoredPosition = Vector2.zero;
    return root.gameObject;
  }

  public void SetEnabled(bool enabled)
  {
    if (_settings.Enabled == enabled)
      return;
    _settings.Enabled = enabled;
    if (!enabled)
      SetSelected(false);
    Apply();
  }

  public void Bind(INativeViewerLayoutHost host)
  {
    if (_host != host && host != null)
      _growthLayout = null;
    _host = host;
    Apply();
    _host?.Select(NativeId, _selected);
  }

  public void SetSelected(bool selected)
  {
    if (_selected == selected)
      return;
    _selected = selected;
    _host?.Select(NativeId, selected);
  }

  public Vector2 PointerInLayoutSpace(Vector2 fallback) =>
    OwnsSelectionOutline && _host.TryGetPointer(out Vector2 normalized)
      ? new Vector2(normalized.x * Screen.width, normalized.y * Screen.height)
      : fallback;

  public void SetBounds(Rect bounds)
  {
    if (!Available || Screen.width <= 0 || Screen.height <= 0)
      return;
    Rect current = Bounds;
    if (
      current.width <= 0
      || !Finite(bounds.x)
      || !Finite(bounds.y)
      || !Finite(bounds.width)
      || !Finite(bounds.height)
      || bounds.width <= 0
      || bounds.height <= 0
    )
      return;
    if (SameBounds(bounds, current))
      return;
    if (_staged && SameBounds(bounds, _stageStartBounds))
    {
      _staged = false;
      Apply();
      return;
    }
    if (!_staged)
      _stageStartBounds = current;
    _staged = true;
    _stagedBounds = bounds;
    Apply();
  }

  public void CommitLayout(bool reselectAnchor, bool initialize = false)
  {
    if ((!_staged && !initialize) || !Available || Screen.width <= 0 || Screen.height <= 0)
      return;
    Rect bounds = Bounds;
    Anchor9 anchor =
      !reselectAnchor && _growthLayout != null
        ? _growthLayout.Anchor
        : NativeViewerGrowth.SelectAnchor(ToBounds(bounds), Screen.width, Screen.height);
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      ToBounds(bounds),
      Screen.width,
      Screen.height,
      anchor
    );
    float denominator = _referenceSize.x * SizeUnit;
    if (!position.IsValid || denominator <= 0 || !Finite(denominator))
      return;
    // Legacy native viewers used AppKit points. Capture the actual rendered size
    // at commit instead of reinterpreting that old scale as reference units.
    float scale = bounds.width / denominator;
    if (!Finite(scale) || scale <= 0)
      return;
    bool preserveScale =
      reselectAnchor && !initialize && _settings.Customized && OverlayLayoutPosition.IsAnchored(_settings);
    _settings.Customized = true;
    _settings.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
    _settings.Anchor = position.Anchor;
    _settings.OffsetX = position.OffsetX;
    _settings.OffsetY = position.OffsetY;
    if (!preserveScale)
      _settings.Scale = scale;
    _growthLayout = NativeViewerGrowth.Capture(ToBounds(bounds), Screen.width, Screen.height, _settings.Scale, anchor);
    _growthKey = GrowthKey;
    _staged = false;
    Apply();
  }

  public void SetPreview(bool preview)
  {
    _preview = preview;
    _setPreview(preview);
    Apply();
  }

  private void Apply()
  {
    if (_applying)
      return;
    _applying = true;
    try
    {
      if (_lastEnabled != Enabled)
      {
        _lastEnabled = Enabled;
        _enabledChanged?.Invoke();
      }
      RefreshBounds();
      TickPreview();
    }
    finally
    {
      _applying = false;
    }
  }

  public void TickPreview()
  {
    // Connected viewers also need geometry refresh: their CSS crop and AppKit
    // backing can change without a Unity resolution change or an IPC message.
    EnsureLayoutCurrent();
    bool visible = _preview && Enabled && !OwnsSelectionOutline && !Hidden;
    if (visible && _previewCanvas == null)
    {
      _previewCanvas = new GameObject("ElementNote offline preview", typeof(RectTransform), typeof(Canvas));
      if (UnityEngine.Application.isPlaying)
        UnityEngine.Object.DontDestroyOnLoad(_previewCanvas);
      Canvas canvas = _previewCanvas.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 32758;
      _previewRoot = NativeViewerMock.Create(_kind, _previewCanvas.transform, _font, true);
      _mock = _previewRoot.GetComponent<NativeViewerMock>();
      _previewBounds = new Rect(float.NaN, 0, 0, 0);
    }
    if (_previewCanvas == null)
      return;
    if (_previewCanvas.activeSelf != visible)
      _previewCanvas.SetActive(visible);
    if (!visible)
      return;
    // Runs every editor frame; touching the RectTransform would rebuild the
    // preview canvas each frame even when nothing moved.
    if (_previewBounds != _bounds)
    {
      _previewBounds = _bounds;
      _previewRoot.anchorMin = _previewRoot.anchorMax = new Vector2(0, 1);
      _previewRoot.pivot = new Vector2(0, 1);
      _previewRoot.anchoredPosition = new Vector2(_bounds.x, -_bounds.y);
      _previewRoot.localScale = new Vector3(
        _bounds.width / NativeViewerMock.Width(_kind),
        _bounds.height / NativeViewerMock.ViewportHeight,
        1
      );
    }
    if (_status != null)
    {
      // The sample is shown only while this viewer has no live page, so an
      // attached session means ElementNote has no tab for this kind.
      NativeViewerStatus status = _status();
      _mock?.SetStatus(status == NativeViewerStatus.Attached ? NativeViewerStatus.NoViewerTab : status);
    }
  }

  public void RefreshLayout()
  {
    _staged = false;
    Apply();
  }

  public void ResetLayout()
  {
    _staged = false;
    _growthLayout = null;
    _settings.Customized = false;
    _settings.LayoutVersion = 0;
    _settings.Anchor = Anchor9.Center;
    _settings.OffsetX = _settings.OffsetY = 0;
    _settings.Scale = 1;
    _settings.CenterX = _settings.CenterY = .5f;
    Apply();
  }

  private void RefreshBounds()
  {
    _screenWidth = Screen.width;
    _screenHeight = Screen.height;
    _screenScale = OverlayLayoutPosition.ScreenScale(_screenWidth, _screenHeight);
    _geometryRevision = _host?.GeometryRevision ?? 0;
    _bounds = default;
    if (_screenWidth <= 0 || _screenHeight <= 0)
      return;
    ReadReferenceSize();
    if (_growthLayout == null || !_growthKey.Equals(GrowthKey))
    {
      Rect initial = InitialBounds();
      float denominator = _referenceSize.x * SizeUnit;
      if (denominator <= 0 || initial.width <= 0)
        return;
      _growthLayout = NativeViewerGrowth.Capture(
        ToBounds(initial),
        _screenWidth,
        _screenHeight,
        initial.width / denominator,
        _settings.Customized
        && OverlayLayoutPosition.IsAnchored(_settings)
        && NativeViewerGrowth.IsAnchor(_settings.Anchor)
          ? _settings.Anchor
          : null
      );
      _growthKey = GrowthKey;
    }
    if (_growthLayout != null)
    {
      _effectiveScale = _staged
        ? _stagedBounds.width / (_referenceSize.x * SizeUnit)
        : Mathf.Min(PositiveScale(_growthLayout.Scale), FitScale());
      float width = _referenceSize.x * SizeUnit * _effectiveScale;
      float height = _referenceSize.y * SizeUnit * _effectiveScale;
      LayoutBounds desired = _staged
        ? new LayoutBounds(_stagedBounds.center.x - width * .5f, _stagedBounds.center.y - height * .5f, width, height)
        : OverlayLayoutPosition.Resolve(_growthLayout, width, height, _screenWidth, _screenHeight);
      LayoutBounds fitted = OverlayLayoutGeometry.KeepVisible(desired, _screenWidth, _screenHeight);
      _bounds = new Rect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
      _host?.LayoutBounds(
        NativeId,
        new Rect(
          _bounds.x / _screenWidth,
          _bounds.y / _screenHeight,
          _bounds.width / _screenWidth,
          _bounds.height / _screenHeight
        ),
        _effectiveScale
      );
      return;
    }
  }

  private float SizeUnit => _sizeUnit > 0 ? _sizeUnit : _screenScale;

  // Reads the content size and size unit from the host. Disconnected, the last
  // known values stay so the placement does not jump.
  private void ReadReferenceSize()
  {
    float desktopUnit = 0;
    if (_host != null && _host.TryGetViewSize(out Vector2 view) && view.x > 0)
      desktopUnit = _screenWidth / view.x;
    if (desktopUnit > 0 && _host.TryGetContentSize(NativeId, out Vector2 content))
    {
      _referenceSize = content;
      _sizeUnit = desktopUnit;
      return;
    }
    if (_host != null && _host.TryGetReferenceSize(NativeId, out Vector2 fitted))
    {
      // A library without desktop units: its fitted size in 1920x1080 units.
      _referenceSize = fitted;
      _sizeUnit = 0;
      return;
    }
    if (_referenceSize.x <= 0 || _referenceSize.y <= 0)
      _referenceSize = new Vector2(NativeViewerMock.Width(_kind), NativeViewerMock.ViewportHeight);
    if (desktopUnit > 0)
      _sizeUnit = desktopUnit;
  }

  // A saved scale larger than the game view is shown at the largest size that fits.
  private float FitScale() =>
    Mathf.Min(_screenWidth / (_referenceSize.x * SizeUnit), _screenHeight / (_referenceSize.y * SizeUnit));

  private (bool, int, Anchor9, float, float, float, float, float) GrowthKey =>
    (
      _settings.Customized,
      _settings.LayoutVersion,
      _settings.Anchor,
      _settings.OffsetX,
      _settings.OffsetY,
      _settings.CenterX,
      _settings.CenterY,
      _settings.Scale
    );

  // Capture the visible placement once. Content/viewport changes do not select
  // a new growth anchor or rewrite saved settings. Undo/redo changes the key.
  private Rect InitialBounds()
  {
    if (_settings.Customized && OverlayLayoutPosition.IsAnchored(_settings))
    {
      float scale = PositiveScale(_settings.Scale) * SizeUnit;
      LayoutBounds resolved = OverlayLayoutGeometry.KeepVisible(
        OverlayLayoutPosition.Resolve(
          _settings,
          _referenceSize.x * scale,
          _referenceSize.y * scale,
          _screenWidth,
          _screenHeight
        ),
        _screenWidth,
        _screenHeight
      );
      return new Rect(resolved.X, resolved.Y, resolved.Width, resolved.Height);
    }

    _host?.Layout(
      NativeId,
      _settings.Customized,
      FiniteClamp(_settings.CenterX, -4, 5, .5f),
      FiniteClamp(_settings.CenterY, -4, 5, .5f),
      FiniteClamp(_settings.Scale, .2f, 5, 1)
    );
    if (_host != null && _host.TryGetBounds(NativeId, out Rect normalized, out _effectiveScale))
    {
      return new Rect(
        normalized.x * _screenWidth,
        normalized.y * _screenHeight,
        normalized.width * _screenWidth,
        normalized.height * _screenHeight
      );
    }
    else
    {
      float width = NativeViewerMock.Width(_kind);
      float height = NativeViewerMock.ViewportHeight;
      float fit = Mathf.Min(1, Mathf.Min(430 / width, 270 / height));
      _effectiveScale = _settings.Customized ? FiniteClamp(_settings.Scale, .2f, 5, 1) : 1;
      width *= fit * _effectiveScale * SizeUnit;
      height *= fit * _effectiveScale * SizeUnit;
      float x =
        _settings.Customized ? FiniteClamp(_settings.CenterX, -4, 5, .5f) * _screenWidth - width / 2
        : _kind == "hand" ? 16
        : _screenWidth - width - 16;
      float y = _settings.Customized
        ? FiniteClamp(_settings.CenterY, -4, 5, .5f) * _screenHeight - height / 2
        : _screenHeight - height - 16;
      LayoutBounds fitted = OverlayLayoutGeometry.KeepVisible(
        new LayoutBounds(x, y, width, height),
        _screenWidth,
        _screenHeight
      );
      return new Rect(fitted.X, fitted.Y, fitted.Width, fitted.Height);
    }
  }

  private void EnsureLayoutCurrent()
  {
    if (_applying)
      return;
    if (
      _screenWidth != Screen.width
      || _screenHeight != Screen.height
      || _geometryRevision != (_host?.GeometryRevision ?? 0)
    )
      Apply();
  }

  private static float PositiveScale(float value) => Finite(value) && value > 0 ? value : 1;

  private static LayoutBounds ToBounds(Rect bounds) => new(bounds.x, bounds.y, bounds.width, bounds.height);

  private static bool SameBounds(Rect left, Rect right) =>
    Mathf.Abs(left.x - right.x) < .01f
    && Mathf.Abs(left.y - right.y) < .01f
    && Mathf.Abs(left.width - right.width) < .01f
    && Mathf.Abs(left.height - right.height) < .01f;

  public void Dispose()
  {
    if (_previewCanvas != null)
    {
      if (UnityEngine.Application.isPlaying)
        UnityEngine.Object.Destroy(_previewCanvas);
      else
        UnityEngine.Object.DestroyImmediate(_previewCanvas);
    }
    _previewCanvas = null;
    _previewRoot = null;
    _mock = null;
  }

  private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

  private static float FiniteClamp(float value, float minimum, float maximum, float fallback) =>
    Finite(value) ? Mathf.Clamp(value, minimum, maximum) : fallback;
}
