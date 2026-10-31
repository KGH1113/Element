using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Shared;

internal sealed class OverlayCanvasHost : IDisposable
{
  public GameObject Root { get; private set; }
  private readonly List<IOverlayLayoutTarget> _layoutTargets = new();
  private readonly List<IOverlayLayoutTarget> _galleryTargets = new();
  private readonly List<RectTransformLayoutTarget> _instances = new();
  private readonly List<RectTransformLayoutTarget> _mirroringInstances = new();
  private List<OverlayLayoutSettings> _layoutSettings = new();
  private bool _visible;
  private bool _layoutPreview;
  public IReadOnlyList<IOverlayLayoutTarget> LayoutTargets => _layoutTargets;
  public IReadOnlyList<IOverlayLayoutTarget> GalleryTargets => _galleryTargets;
  internal List<OverlayLayoutSettings> LayoutSettings => _layoutSettings;

  public OverlayCanvasHost()
  {
    Root = new GameObject("Element Overlay");
    Object.DontDestroyOnLoad(Root);

    Canvas canvas = Root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;

    CanvasScaler scaler = Root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(
      OverlayLayoutPosition.ReferenceWidth,
      OverlayLayoutPosition.ReferenceHeight
    );
    scaler.matchWidthOrHeight = 0.5f;

    Root.AddComponent<OverlayViewportWatcher>().Initialize(this);

    Root.SetActive(false);
  }

  public void SetVisible(bool visible)
  {
    _visible = visible;
    if (Root != null)
      Root.SetActive(_visible || _layoutPreview);
  }

  public void ConfigureLayouts(List<OverlayLayoutSettings> layouts)
  {
    _layoutSettings = layouts ?? new List<OverlayLayoutSettings>();
    for (int i = _instances.Count - 1; i >= 0; i--)
    {
      RectTransformLayoutTarget target = _instances[i];
      OverlayLayoutSettings settings = FindSettings(target.Id);
      if (settings != null && settings.TemplateId == target.TemplateId)
        target.Configure(settings);
      else
      {
        target.EnabledChanged -= UpdateMirrorMembership;
        target.Dispose();
        _layoutTargets.Remove(target);
        _mirroringInstances.Remove(target);
        _instances.RemoveAt(i);
      }
    }
    foreach (RectTransformLayoutTarget target in _galleryTargets)
    {
      target.Configure(GetSettings(target.Id));
      RestoreInstances(target);
    }
  }

  public void RegisterLayout(string id, string label, GameObject root, TextMeshProUGUI text, string preview)
  {
    RectTransformLayoutTarget target = new(
      id,
      label,
      root.GetComponent<RectTransform>(),
      Root.transform,
      text,
      preview
    );
    _layoutTargets.Add(target);
    _galleryTargets.Add(target);
    target.Configure(GetSettings(id));
    RestoreInstances(target);
  }

  public IOverlayLayoutTarget AddElement(string templateId)
  {
    foreach (RectTransformLayoutTarget template in _galleryTargets)
      if (template.TemplateId == templateId)
      {
        OverlayLayoutSettings settings = new()
        {
          Id = templateId + ":" + Guid.NewGuid().ToString("N"),
          TemplateId = templateId,
        };
        _layoutSettings.Add(settings);
        return CreateInstance(template, settings);
      }
    return null;
  }

  private RectTransformLayoutTarget CreateInstance(RectTransformLayoutTarget template, OverlayLayoutSettings settings)
  {
    RectTransformLayoutTarget instance = template.CreateInstance(settings.Id, Root.transform);
    instance.Configure(settings);
    _instances.Add(instance);
    instance.EnabledChanged += UpdateMirrorMembership;
    UpdateMirrorMembership(instance);
    _layoutTargets.Add(instance);
    instance.SetPreview(_layoutPreview);
    return instance;
  }

  private void UpdateMirrorMembership(RectTransformLayoutTarget target)
  {
    if (!target.Enabled)
      _mirroringInstances.Remove(target);
    else if (!_mirroringInstances.Contains(target))
      _mirroringInstances.Add(target);
  }

  private void RestoreInstances(RectTransformLayoutTarget template)
  {
    // Deleted copies are kept disabled only for the editor's undo history.
    // Nothing can restore them after a restart, so drop them instead of
    // rebuilding invisible objects on every launch.
    _layoutSettings.RemoveAll(settings => IsCopy(template, settings) && !settings.Enabled);
    foreach (OverlayLayoutSettings settings in _layoutSettings)
    {
      if (!IsCopy(template, settings))
        continue;
      bool exists = false;
      foreach (RectTransformLayoutTarget instance in _instances)
        if (instance.Id == settings.Id)
        {
          exists = true;
          break;
        }
      if (!exists)
        CreateInstance(template, settings);
    }
  }

  private static bool IsCopy(RectTransformLayoutTarget template, OverlayLayoutSettings settings) =>
    settings != null
    && !string.IsNullOrEmpty(settings.Id)
    && settings.Id != template.Id
    && settings.TemplateId == template.TemplateId;

  /// <summary>
  /// Destroys copies deleted or undone during an editing session. Call only after
  /// the editor's history is discarded; undo needs the disabled instances.
  /// </summary>
  public void PruneDisabledInstances()
  {
    for (int i = _instances.Count - 1; i >= 0; i--)
    {
      RectTransformLayoutTarget target = _instances[i];
      if (target.Enabled)
        continue;
      target.EnabledChanged -= UpdateMirrorMembership;
      target.Dispose();
      _layoutTargets.Remove(target);
      _mirroringInstances.Remove(target);
      _instances.RemoveAt(i);
      _layoutSettings.RemoveAll(settings => settings != null && settings.Id == target.Id);
    }
  }

  public void SetLayoutPreview(bool preview)
  {
    _layoutPreview = preview;
    SetVisible(_visible);
    TickMirrors();
    foreach (IOverlayLayoutTarget target in _layoutTargets)
      target.SetPreview(preview);
  }

  public void ApplyLayouts()
  {
    foreach (RectTransformLayoutTarget target in _layoutTargets)
      target.Apply();
  }

  public void TickLayoutPreview()
  {
    if (!_layoutPreview)
      return;
    TickMirrors();
    foreach (RectTransformLayoutTarget target in _layoutTargets)
      target.UpdatePreview();
  }

  internal void TickMirrors()
  {
    foreach (RectTransformLayoutTarget instance in _mirroringInstances)
    {
      instance.UpdateMirroredVisual();
      if (_layoutPreview)
        instance.UpdatePreview();
    }
  }

  public void ResetLayouts()
  {
    foreach (OverlayLayoutSettings settings in _layoutSettings)
    {
      if (settings == null)
        continue;
      settings.Customized = false;
      settings.LayoutVersion = 0;
      settings.Anchor = Anchor9.Center;
      settings.OffsetX = settings.OffsetY = 0;
      settings.CenterX = settings.CenterY = 0.5f;
      settings.Scale = 1f;
    }
    ApplyLayouts();
  }

  private OverlayLayoutSettings GetSettings(string id)
  {
    OverlayLayoutSettings found = FindSettings(id);
    if (found != null)
      return found;
    OverlayLayoutSettings created = new() { Id = id };
    _layoutSettings.Add(created);
    return created;
  }

  private OverlayLayoutSettings FindSettings(string id)
  {
    foreach (OverlayLayoutSettings settings in _layoutSettings)
      if (settings != null && settings.Id == id)
        return settings;
    return null;
  }

  public void Dispose()
  {
    if (Root == null)
      return;

    Root.SetActive(false);
    foreach (RectTransformLayoutTarget target in _layoutTargets)
    {
      target.EnabledChanged -= UpdateMirrorMembership;
      target.Dispose();
    }
    _layoutTargets.Clear();
    _galleryTargets.Clear();
    _instances.Clear();
    _mirroringInstances.Clear();
    Object.Destroy(Root);
    Root = null;
  }
}

internal sealed class OverlayViewportWatcher : MonoBehaviour
{
  private OverlayCanvasHost _host;
  private int _width;
  private int _height;
  private Canvas _canvas;
  private float _scale;
  private bool _viewportDirty = true;

  public void Initialize(OverlayCanvasHost host)
  {
    _host = host;
    _canvas = GetComponent<Canvas>();
  }

  private void OnEnable() => Canvas.willRenderCanvases += ApplyViewport;

  private void OnDisable() => Canvas.willRenderCanvases -= ApplyViewport;

  private void LateUpdate()
  {
    if (_host == null)
      return;
    _host.TickMirrors();
    float scale = _canvas != null ? _canvas.scaleFactor : 1;
    if (_width == Screen.width && _height == Screen.height && _scale == scale)
      return;
    _width = Screen.width;
    _height = Screen.height;
    _scale = scale;
    _viewportDirty = true;
  }

  private void ApplyViewport()
  {
    if (!_viewportDirty || _host == null)
      return;
    _viewportDirty = false;
    // CanvasScaler has applied the viewport scale before this render callback.
    // Reposition only on a viewport change; never force a global Canvas rebuild.
    _host.ApplyLayouts();
  }
}
