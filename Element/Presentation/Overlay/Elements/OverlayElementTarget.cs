using System;
using Element.Configuration;
using Element.Domain.Layout;
using Element.Presentation.Layout;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// Placement shared by text and shape elements: anchored offsets and a uniform
/// scale, staged while the pointer edits and saved only on commit. Content
/// bounds, not the edit rectangle, carry the anchor, so a value growing wider
/// keeps the chosen edge in place.
/// </summary>
internal abstract class OverlayElementTarget : IOverlayLayoutTarget, IDisposable
{
  private readonly Vector3[] _corners = new Vector3[4];
  private readonly OverlayLayoutSettings _stagedSettings = new();
  private bool _staged;
  private Rect _stageOrigin;
  private bool _applying;

  protected OverlayElementTarget(Transform parent, string name)
  {
    GameObject root = new(name, typeof(RectTransform));
    Rect = root.GetComponent<RectTransform>();
    Rect.SetParent(parent, false);
  }

  protected RectTransform Rect { get; private set; }
  public OverlayLayoutSettings Settings { get; private set; }
  public string Id => Settings?.Id ?? string.Empty;
  public string TemplateId => Settings?.TemplateId ?? string.Empty;
  public abstract string Label { get; }
  public bool Available => Rect != null;
  public bool Enabled => Settings == null || Settings.Enabled;
  public float EffectiveScale => Rect != null ? Rect.localScale.x : 1f;

  /// <summary>Pending placement fits run on their first layout; the host fits these.</summary>
  public bool PendingFit => !string.IsNullOrEmpty(Settings?.PendingGroup);

  // Physical pixels, origin at the top left.
  public virtual Rect Bounds => RectBounds;
  public virtual Rect SelectionBounds => Enabled ? Bounds : default;

  protected Rect RectBounds
  {
    get
    {
      if (Rect == null)
        return default;
      Rect.GetWorldCorners(_corners);
      Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(null, _corners[1]);
      Vector2 bottomRight = RectTransformUtility.WorldToScreenPoint(null, _corners[3]);
      return new Rect(topLeft.x, Screen.height - topLeft.y, bottomRight.x - topLeft.x, topLeft.y - bottomRight.y);
    }
  }

  // The content in the element's local space; false until it has been laid out.
  protected abstract bool TryGetLocalContent(out Rect local);

  internal Rect AnchorBounds
  {
    get
    {
      if (Rect == null || !TryGetLocalContent(out Rect local))
        return RectBounds;
      Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
      Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
      for (int i = 0; i < 4; i++)
      {
        Vector3 point = new((i & 1) == 0 ? local.xMin : local.xMax, i < 2 ? local.yMin : local.yMax, 0);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, Rect.TransformPoint(point));
        screen.y = Screen.height - screen.y;
        minimum = Vector2.Min(minimum, screen);
        maximum = Vector2.Max(maximum, screen);
      }
      return UnityEngine.Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
    }
  }

  public bool HasContent => Rect != null && TryGetLocalContent(out _);

  public void Configure(OverlayLayoutSettings settings)
  {
    Settings = settings;
    Apply();
  }

  /// <summary>Re-reads content and placement, dropping any staged gesture.</summary>
  public void Apply()
  {
    if (Rect == null || Settings == null)
      return;
    _staged = false;
    ApplyContent();
    ApplyPosition();
  }

  public void RefreshLayout() => Apply();

  // Text runs or shape geometry changed in Settings.
  protected abstract void ApplyContent();

  // Anchors, pivot, size and scale of the unplaced element: a converted
  // built-in's original place, or the center of the screen.
  protected abstract void ResetPose();

  public virtual void SetPreview(bool preview) { }

  public void SetEnabled(bool enabled)
  {
    if (Settings == null)
      return;
    Settings.Enabled = enabled;
    ApplyVisibility();
  }

  public void ResetLayout()
  {
    if (Settings == null)
      return;
    Domain.Elements.OverlayElementDefaults.ResetPlacement(Settings);
    Apply();
  }

  protected void ApplyPosition()
  {
    if (Rect == null || Settings == null || _applying)
      return;
    _applying = true;
    try
    {
      ApplyVisibility();
      ResetPose();
      OverlayLayoutSettings settings = _staged ? _stagedSettings : Settings;
      // Fitting a converted combo needs the built-in arrangement first.
      if (!settings.Customized || PendingFit)
        return;
      bool anchored = OverlayLayoutPosition.IsAnchored(settings);
      float scale = ValidScale(settings.Scale, anchored);
      Rect.localScale = new Vector3(scale, scale, 1);
      FitVisibleSize();
      // Legacy centers refer to the edit rectangle; anchors refer to content.
      Rect positioned = anchored ? AnchorBounds : RectBounds;
      LayoutBounds resolved = OverlayLayoutPosition.Resolve(
        settings,
        positioned.width,
        positioned.height,
        Screen.width,
        Screen.height
      );
      Rect.position += new Vector3(resolved.X - positioned.x, positioned.y - resolved.Y, 0);
      Rect bounds = Bounds;
      LayoutBounds visible = OverlayLayoutGeometry.KeepVisible(
        new LayoutBounds(bounds.x, bounds.y, bounds.width, bounds.height),
        Screen.width,
        Screen.height
      );
      MoveCenter(new Vector2(visible.X + visible.Width * .5f, visible.Y + visible.Height * .5f));
    }
    finally
    {
      _applying = false;
    }
  }

  private void ApplyVisibility()
  {
    bool enabled = Enabled;
    if (Rect.gameObject.activeSelf != enabled)
      Rect.gameObject.SetActive(enabled);
  }

  public void SetBounds(Rect bounds)
  {
    if (Rect == null || Settings == null || bounds.width <= 0f || bounds.height <= 0f)
      return;
    Rect current = Bounds;
    if (
      current.width <= 0f
      || !OverlayLayoutGeometry.Finite(bounds.center.x)
      || !OverlayLayoutGeometry.Finite(bounds.center.y)
    )
      return;
    if (Same(current, bounds))
      return;
    BeginStage();
    float scale = ValidScale(Rect.localScale.x * bounds.width / current.width, true);
    Rect.localScale = new Vector3(scale, scale, 1);
    FitVisibleSize();
    StageCenter(bounds.center);
  }

  /// <summary>Remembers where a gesture started, once per gesture.</summary>
  protected void BeginStage()
  {
    if (!_staged)
      _stageOrigin = Bounds;
  }

  /// <summary>Moves the element so its bounds center on <paramref name="center"/>, staged.</summary>
  protected void StageCenter(Vector2 center)
  {
    Rect fitted = Bounds;
    LayoutBounds visible = OverlayLayoutGeometry.KeepVisible(
      new LayoutBounds(center.x - fitted.width * .5f, center.y - fitted.height * .5f, fitted.width, fitted.height),
      Screen.width,
      Screen.height
    );
    MoveCenter(new Vector2(visible.X + visible.Width * .5f, visible.Y + visible.Height * .5f));
    if (Same(Bounds, _stageOrigin) && !ContentChangedWhileStaged)
    {
      _staged = false;
      return;
    }
    Rect content = AnchorBounds;
    OverlayLayoutSettings previous = _staged ? _stagedSettings : Settings;
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      new LayoutBounds(content.x, content.y, content.width, content.height),
      Screen.width,
      Screen.height,
      OverlayLayoutPosition.IsAnchored(previous) ? previous.Anchor : null
    );
    if (!position.IsValid)
      return;
    StorePosition(_stagedSettings, position);
    _stagedSettings.Scale = EffectiveScale;
    _staged = true;
  }

  // Shapes resize their box in place; the staged placement must still commit.
  protected virtual bool ContentChangedWhileStaged => false;

  public void CommitLayout(bool reselectAnchor, bool initialize = false)
  {
    if ((!_staged && !initialize) || Settings == null || Rect == null)
      return;
    Rect content = AnchorBounds;
    AnchoredLayoutPosition position = OverlayLayoutPosition.Capture(
      new LayoutBounds(content.x, content.y, content.width, content.height),
      Screen.width,
      Screen.height,
      !reselectAnchor && OverlayLayoutPosition.IsAnchored(Settings) ? Settings.Anchor : null
    );
    if (!position.IsValid)
      return;
    bool preserveScale =
      reselectAnchor
      && !initialize
      && OverlayLayoutPosition.IsAnchored(Settings)
      && OverlayLayoutGeometry.Finite(Settings.Scale)
      && Settings.Scale > 0;
    float scale = preserveScale ? Settings.Scale : EffectiveScale;
    StorePosition(Settings, position);
    Vector2 center = Bounds.center;
    Settings.CenterX = center.x / Mathf.Max(1, Screen.width);
    Settings.CenterY = center.y / Mathf.Max(1, Screen.height);
    Settings.Scale = scale;
    Settings.PendingGroup = string.Empty;
    _staged = false;
  }

  /// <summary>Scales about the pose's pivot, then moves the content to <paramref name="content"/>.</summary>
  internal void PlaceContent(Rect content, float scale)
  {
    Rect.localScale = new Vector3(scale, scale, 1);
    Rect current = AnchorBounds;
    Rect.position += new Vector3(content.center.x - current.center.x, current.center.y - content.center.y, 0);
  }

  private static void StorePosition(OverlayLayoutSettings settings, AnchoredLayoutPosition position)
  {
    settings.Customized = true;
    settings.LayoutVersion = OverlayLayoutPosition.CurrentVersion;
    settings.Anchor = position.Anchor;
    settings.OffsetX = position.OffsetX;
    settings.OffsetY = position.OffsetY;
  }

  protected void MoveCenter(Vector2 center)
  {
    Rect bounds = Bounds;
    Rect.position += new Vector3(center.x - bounds.center.x, bounds.center.y - center.y, 0f);
  }

  private void FitVisibleSize()
  {
    Rect bounds = Bounds;
    if (bounds.width <= 0 || bounds.height <= 0 || Screen.width <= 0 || Screen.height <= 0)
      return;
    float visible = OverlayLayoutGeometry.MinimumVisibleFraction;
    float fit = Mathf.Min(
      1,
      Mathf.Min(Screen.width / (bounds.width * visible), Screen.height / (bounds.height * visible))
    );
    Rect.localScale *= fit;
  }

  private static float ValidScale(float scale, bool anchored) =>
    OverlayLayoutGeometry.Finite(scale) && scale > 0 ? (anchored ? scale : Mathf.Clamp(scale, 0.2f, 5f)) : 1f;

  // New content geometry follows an anchored element, except while a gesture
  // owns its placement.
  protected void ContentSizeChanged()
  {
    if (!_applying && !_staged && Settings != null && OverlayLayoutPosition.IsAnchored(Settings) && !PendingFit)
      ApplyPosition();
    ContentChanged?.Invoke(this);
  }

  internal event Action<OverlayElementTarget> ContentChanged;

  protected static void SetPose(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
  {
    if (rect.anchorMin != anchor)
      rect.anchorMin = anchor;
    if (rect.anchorMax != anchor)
      rect.anchorMax = anchor;
    if (rect.pivot != pivot)
      rect.pivot = pivot;
    if (rect.sizeDelta != size)
      rect.sizeDelta = size;
    if (rect.anchoredPosition != position)
      rect.anchoredPosition = position;
  }

  private static bool Same(Rect a, Rect b) =>
    Mathf.Abs(a.x - b.x) < .001f
    && Mathf.Abs(a.y - b.y) < .001f
    && Mathf.Abs(a.width - b.width) < .001f
    && Mathf.Abs(a.height - b.height) < .001f;

  public virtual void Dispose()
  {
    if (Rect != null)
      Presentation.Settings.SettingsUiFactory.Destroy(Rect.gameObject);
    Rect = null;
  }
}
