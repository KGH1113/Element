using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Migration;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Overlay.Shared;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Element.Application.Layout;
using Element.Application.Mods;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>
/// The overlay canvas and its elements, kept in step with the saved layout
/// list. Each kind of element makes its own elements on its own layer:
/// shapes below other mods' elements, and those below every text.
/// </summary>
internal sealed class OverlayCanvasHost : IDisposable
{
  public GameObject Root { get; private set; }
  public OverlayValues Values { get; } = new();
  private readonly List<OverlayElementTarget> _elements = new();
  private readonly HashSet<string> _defaultIds = new();
  private readonly OverlayElementKind[] _kinds;
  private readonly ModElementKind _mods;
  private readonly ComboGroupFit _comboFit;
  private readonly Action<OverlayElementTarget> _contentChanged;
  private List<OverlayLayoutSettings> _layoutSettings = new();
  private OverlayFonts _fonts;
  private bool _visible;
  private bool _layoutPreview;
  private int _settles;

  /// <summary>Values from outside the game's events, written into <see cref="Values"/> each frame.</summary>
  public IOverlayValueFeed Feed { get; set; }

  /// <summary>The element types other mods add; without it their elements wait as if the mod were missing.</summary>
  public ModElements ModElements { get; set; }

  public IReadOnlyList<IOverlayLayoutTarget> LayoutTargets => _elements;
  internal List<OverlayLayoutSettings> LayoutSettings => _layoutSettings;
  public bool CanShowText => _fonts != null;

  /// <summary>The fonts texts draw with; null when the bundled font could not load.</summary>
  public OverlayFonts Fonts => _fonts;

  /// <summary>The converted built-ins: kept hidden when deleted, so Reset all can bring them back.</summary>
  public IReadOnlyCollection<string> DefaultIds => _defaultIds;

  public OverlayCanvasHost()
  {
    Root = new GameObject("Element Overlay");
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(Root);

    UnityEngine.Canvas canvas = Root.AddComponent<UnityEngine.Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;

    CanvasScaler scaler = Root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(
      OverlayLayoutPosition.ReferenceWidth,
      OverlayLayoutPosition.ReferenceHeight
    );
    scaler.matchWidthOrHeight = 0.5f;

    // Frame order: texts, shapes, then other mods' elements.
    TextElementKind texts = new(this);
    _mods = new ModElementKind(this);
    _kinds = new OverlayElementKind[] { texts, new ShapeElementKind(this), _mods };
    for (int depth = 0; depth < _kinds.Length; depth++)
      foreach (OverlayElementKind kind in _kinds)
        if (kind.LayerDepth == depth)
          kind.Layer = CreateLayer(kind.LayerName);
    _comboFit = new ComboGroupFit(texts.Elements);
    _contentChanged = OnContentChanged;
    foreach (OverlayDefaultElement element in OverlayElementDefaults.All)
      _defaultIds.Add(element.Id);

    Root.AddComponent<OverlayViewportWatcher>().Initialize(this);

    Root.SetActive(false);
  }

  /// <summary>Text elements need the overlay fonts; without them only shapes are shown.</summary>
  public void SetFonts(OverlayFonts fonts)
  {
    _fonts = fonts;
    foreach (OverlayElementKind kind in _kinds)
      kind.SetFonts(fonts);
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
      Create(settings);
    }
    _comboFit.Pending = true;
  }

  /// <summary>
  /// Adds <paramref name="settings"/>, made by its kind (see
  /// <see cref="TextElementKind.NewSettings"/> and the others), and its
  /// element; null when no kind can draw it now.
  /// </summary>
  public OverlayElementTarget Add(OverlayLayoutSettings settings)
  {
    OverlayElementKind kind = settings == null ? null : KindOf(settings.TemplateId);
    if (kind == null || !kind.CanCreate)
      return null;
    _layoutSettings.Add(settings);
    return Create(settings);
  }

  private OverlayElementTarget Create(OverlayLayoutSettings settings)
  {
    OverlayElementKind kind = KindOf(settings.TemplateId);
    if (kind == null || !kind.CanCreate)
      return null;
    OverlayElementTarget element = kind.Create();
    element.ContentChanged += _contentChanged;
    _elements.Add(element);
    element.SetPreview(_layoutPreview);
    element.Configure(settings);
    return element;
  }

  private OverlayElementKind KindOf(string templateId)
  {
    foreach (OverlayElementKind kind in _kinds)
      if (kind.Owns(templateId))
        return kind;
    return null;
  }

  private void Remove(OverlayElementTarget element)
  {
    element.ContentChanged -= _contentChanged;
    element.Dispose();
    _elements.Remove(element);
    foreach (OverlayElementKind kind in _kinds)
      if (kind.Forget(element))
        break;
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
    // Fonts tried in the editor and no longer used go with them; other mods' elements keep theirs.
    _fonts?.Retain(_layoutSettings, _mods.UsedFonts());
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
    foreach (OverlayElementKind kind in _kinds)
      kind.Applied();
  }

  public void TickLayoutPreview() => Tick();

  /// <summary>Once per frame while the canvas is shown: redraws what changed.</summary>
  internal void Tick()
  {
    _fonts?.Tick();
    Feed?.Pump(Values);
    OverlayValueMask changes = Values.TakeChanges();
    bool settle = _settles != Values.Settles;
    _settles = Values.Settles;
    // Each refresh is a mask test unless its values, the theme or its scale changed.
    for (int i = 0; i < _kinds.Length; i++)
      _kinds[i].Tick(changes, settle);
    if (_comboFit.Pending)
      _comboFit.Fit();
  }

  private void OnContentChanged(OverlayElementTarget element)
  {
    if (element.PendingFit)
      _comboFit.Pending = true;
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
      element.ContentChanged -= _contentChanged;
      element.Dispose();
    }
    _elements.Clear();
    foreach (OverlayElementKind kind in _kinds)
      kind.Clear();
    Settings.SettingsUiFactory.Destroy(Root);
    Root = null;
  }
}
