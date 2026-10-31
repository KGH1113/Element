using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Elements;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Overlay.Shared;

/// <summary>
/// The overlay canvas and its text and shape elements, kept in step with the
/// saved layout list. Shapes draw on a layer below every text.
/// </summary>
internal sealed class OverlayCanvasHost : IDisposable
{
  public GameObject Root { get; private set; }
  public OverlayValues Values { get; } = new();
  private readonly List<OverlayElementTarget> _elements = new();
  private readonly List<TextOverlayElement> _texts = new();
  private readonly List<ShapeOverlayElement> _shapes = new();
  private readonly HashSet<string> _defaultIds = new();
  private Transform _shapeLayer;
  private Transform _textLayer;
  private List<OverlayLayoutSettings> _layoutSettings = new();
  private TMP_FontAsset _font;
  private Material _material;
  private bool _visible;
  private bool _layoutPreview;
  private bool _fitPending;
  private int _comboBumps;
  private int _themeVersion = -1;

  public IReadOnlyList<IOverlayLayoutTarget> LayoutTargets => _elements;
  internal List<OverlayLayoutSettings> LayoutSettings => _layoutSettings;
  public bool CanShowText => _font != null;
  public TMP_FontAsset Font => _font;

  /// <summary>The converted built-ins: kept hidden when deleted, so Reset all can bring them back.</summary>
  public IReadOnlyCollection<string> DefaultIds => _defaultIds;

  public OverlayCanvasHost()
  {
    Root = new GameObject("Element Overlay");
    if (UnityEngine.Application.isPlaying)
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

    _shapeLayer = CreateLayer("Shapes");
    _textLayer = CreateLayer("Texts");
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
      _defaultIds.Add(element.Id);

    Root.AddComponent<OverlayViewportWatcher>().Initialize(this);

    Root.SetActive(false);
  }

  /// <summary>Text elements need the overlay font; without it only shapes are shown.</summary>
  public void SetFont(TMP_FontAsset font, Material material)
  {
    _font = font;
    _material = material;
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
    for (int i = _elements.Count - 1; i >= 0; i--)
    {
      OverlayElementTarget element = _elements[i];
      OverlayLayoutSettings settings = FindSettings(element.Id);
      if (settings != null && settings.TemplateId == element.TemplateId)
        element.Configure(settings);
      else
        Remove(element);
    }
    foreach (OverlayLayoutSettings settings in _layoutSettings)
    {
      if (settings == null || string.IsNullOrEmpty(settings.Id) || Find(settings.Id) != null)
        continue;
      if (settings.TemplateId == OverlayElementKinds.Text)
        CreateText(settings);
      else if (settings.TemplateId == OverlayElementKinds.Shape)
        CreateShape(settings);
    }
    _fitPending = true;
  }

  public TextOverlayElement AddText(List<OverlayTextRun> runs)
  {
    if (!CanShowText)
      return null;
    OverlayLayoutSettings settings = new()
    {
      Id = OverlayElementKinds.Text + ":" + Guid.NewGuid().ToString("N"),
      TemplateId = OverlayElementKinds.Text,
      Text = new OverlayTextSettings { Runs = runs ?? new List<OverlayTextRun>() },
    };
    _layoutSettings.Add(settings);
    return CreateText(settings);
  }

  public ShapeOverlayElement AddShape()
  {
    OverlayLayoutSettings settings = new()
    {
      Id = OverlayElementKinds.Shape + ":" + Guid.NewGuid().ToString("N"),
      TemplateId = OverlayElementKinds.Shape,
      Shape = OverlayShapeGeometry.Create(),
    };
    _layoutSettings.Add(settings);
    return CreateShape(settings);
  }

  private TextOverlayElement CreateText(OverlayLayoutSettings settings)
  {
    if (!CanShowText)
      return null;
    TextOverlayElement element = new(_textLayer, _font, _material, Values);
    element.ContentChanged += OnContentChanged;
    _elements.Add(element);
    _texts.Add(element);
    element.SetPreview(_layoutPreview);
    element.Configure(settings);
    return element;
  }

  private ShapeOverlayElement CreateShape(OverlayLayoutSettings settings)
  {
    ShapeOverlayElement element = new(_shapeLayer);
    _elements.Add(element);
    _shapes.Add(element);
    element.Configure(settings);
    return element;
  }

  private void Remove(OverlayElementTarget element)
  {
    element.ContentChanged -= OnContentChanged;
    element.Dispose();
    _elements.Remove(element);
    if (element is TextOverlayElement text)
      _texts.Remove(text);
    if (element is ShapeOverlayElement shape)
      _shapes.Remove(shape);
  }

  /// <summary>
  /// Destroys elements deleted or undone during an editing session. Call only
  /// after the editor's history is discarded; undo needs the hidden elements.
  /// </summary>
  public void PruneDisabledInstances()
  {
    for (int i = _elements.Count - 1; i >= 0; i--)
    {
      OverlayElementTarget element = _elements[i];
      if (element.Enabled || _defaultIds.Contains(element.Id))
        continue;
      string id = element.Id;
      Remove(element);
      _layoutSettings.RemoveAll(settings => settings != null && settings.Id == id);
    }
  }

  public void SetLayoutPreview(bool preview)
  {
    _layoutPreview = preview;
    SetVisible(_visible);
    foreach (OverlayElementTarget element in _elements)
      element.SetPreview(preview);
  }

  public void ApplyLayouts()
  {
    foreach (OverlayElementTarget element in _elements)
      element.Apply();
    foreach (ShapeOverlayElement shape in _shapes)
      shape.Refresh();
  }

  public void TickLayoutPreview() => Tick();

  /// <summary>Once per frame while the canvas is shown: redraws what changed.</summary>
  internal void Tick()
  {
    ulong changes = Values.TakeChanges();
    bool themeChanged = _themeVersion != OverlayTheme.Version;
    _themeVersion = OverlayTheme.Version;
    if (changes != 0 || themeChanged)
      foreach (TextOverlayElement text in _texts)
        text.Refresh(changes);
    if (themeChanged)
      foreach (ShapeOverlayElement shape in _shapes)
        shape.Refresh();
    if (_comboBumps != Values.ComboBumps)
    {
      _comboBumps = Values.ComboBumps;
      foreach (TextOverlayElement text in _texts)
        if (text.UsesCombo && text.Enabled)
          text.Pulse();
    }
    if (_fitPending)
      FitPendingGroups();
  }

  private void OnContentChanged(OverlayElementTarget element)
  {
    if (element.PendingFit)
      _fitPending = true;
  }

  // A moved combo was one element. Measure its title and number where the
  // built-in drew them, then place both as the old combo was placed.
  private void FitPendingGroups()
  {
    _fitPending = false;
    foreach (TextOverlayElement first in _texts)
    {
      if (!first.PendingFit)
        continue;
      string group = first.Settings.PendingGroup;
      List<TextOverlayElement> members = _texts.FindAll(text => text.PendingFit && text.Settings.PendingGroup == group);
      bool ready = true;
      Rect union = default;
      for (int i = 0; i < members.Count; i++)
      {
        if (!members[i].Enabled || !members[i].HasContent)
        {
          ready = false;
          break;
        }
        Rect bounds = members[i].AnchorBounds;
        union =
          i == 0
            ? bounds
            : Rect.MinMaxRect(
              Mathf.Min(union.xMin, bounds.xMin),
              Mathf.Min(union.yMin, bounds.yMin),
              Mathf.Max(union.xMax, bounds.xMax),
              Mathf.Max(union.yMax, bounds.yMax)
            );
      }
      if (!ready)
        continue;
      OverlayLayoutSettings old = first.Settings;
      bool anchored = OverlayLayoutPosition.IsAnchored(old);
      float scale =
        OverlayLayoutGeometry.Finite(old.Scale) && old.Scale > 0
          ? (anchored ? old.Scale : Mathf.Clamp(old.Scale, .2f, 5f))
          : 1;
      Vector2 from,
        to;
      if (anchored)
      {
        LayoutBounds target = OverlayLayoutPosition.Resolve(
          old,
          union.width * scale,
          union.height * scale,
          Screen.width,
          Screen.height
        );
        from = union.min;
        to = new Vector2(target.X, target.Y);
      }
      else
      {
        float unit = OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
        LegacyOverlayPlacement root = LegacyOverlayPlacement.ComboRoot;
        from = new Vector2(Screen.width * .5f, (-root.Position.y + root.Size.y * .5f) * unit);
        to = new Vector2(old.CenterX * Screen.width, old.CenterY * Screen.height);
      }
      List<Rect> targets = new(members.Count);
      foreach (TextOverlayElement member in members)
      {
        Rect bounds = member.AnchorBounds;
        Vector2 center = to + (bounds.center - from) * scale;
        targets.Add(new Rect(center - bounds.size * scale * .5f, bounds.size * scale));
      }
      for (int i = 0; i < members.Count; i++)
      {
        members[i].PlaceContent(targets[i], members[i].EffectiveScale * scale);
        members[i].CommitLayout(true, initialize: true);
      }
      _fitPending = true;
      return;
    }
  }

  private Transform CreateLayer(string name)
  {
    GameObject layer = new(name, typeof(RectTransform));
    RectTransform rect = layer.GetComponent<RectTransform>();
    rect.SetParent(Root.transform, false);
    rect.anchorMin = Vector2.zero;
    rect.anchorMax = Vector2.one;
    rect.offsetMin = rect.offsetMax = Vector2.zero;
    return rect;
  }

  private OverlayElementTarget Find(string id)
  {
    foreach (OverlayElementTarget element in _elements)
      if (element.Id == id)
        return element;
    return null;
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
    foreach (OverlayElementTarget element in _elements)
    {
      element.ContentChanged -= OnContentChanged;
      element.Dispose();
    }
    _elements.Clear();
    _texts.Clear();
    _shapes.Clear();
    Settings.SettingsUiFactory.Destroy(Root);
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
    _host.Tick();
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
