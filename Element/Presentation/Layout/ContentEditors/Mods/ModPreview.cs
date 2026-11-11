using System;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// The live preview on a mod element's editing screen: a second instance of
/// the mod's element, at its in-game size unless that doesn't fit, showing
/// each change as it happens; while Grow from is open, a dot marks its point.
/// </summary>
internal sealed class ModPreview
{
  private readonly ModFailureGuard _guard;
  private RectTransform _holder;
  private RectTransform _root;
  private ModDrawing _drawing;
  private OverlayElement _instance;
  private Action _update;
  private UiCapsuleGraphic _growMark;
  private Rect _area;
  private float _elementScale = 1;
  private Vector2 _boxTopLeft;
  private float _unitsToFrame = 1;

  public ModPreview(ModFailureGuard guard) => _guard = guard;

  /// <summary>The mod's preview instance; null when it couldn't be made.</summary>
  public OverlayElement Instance => _instance;

  /// <summary>The element's own scale, which the preview draws at.</summary>
  public float ElementScale
  {
    set => _elementScale = value;
  }

  /// <summary>The area the preview fits in (frame units).</summary>
  public Rect Area => _area;

  /// <summary>Its holder on the stage, in <paramref name="area"/>.</summary>
  public void BuildHolder(UiFactory ui, OverlayEditorFrame screen, Rect area, OverlayFonts fonts)
  {
    _area = area;
    _holder = ui.Rect("Preview", screen.Stage, 0, 0, 10, 10);
    _holder.anchorMin = _holder.anchorMax = _holder.pivot = new Vector2(0, 1);
    _drawing = new ModDrawing(fonts);
  }

  public void BuildGrowMark(UiFactory ui, RectTransform frame)
  {
    _growMark = ui.Capsule("Grow point", frame, 0, 0, 12, 12, UiPalette.SoftAccent);
    _growMark.gameObject.SetActive(false);
  }

  /// <summary>A second instance of <paramref name="type"/>, with <paramref name="data"/>; false when the mod failed.</summary>
  public bool Create(ElementClass type, OverlayModSettings settings, string data)
  {
    OverlayElement instance = type.Create(out Exception error);
    if (instance == null)
    {
      _guard.Failed("being made", error);
      return false;
    }
    GameObject root = new("Mod drawing", typeof(RectTransform));
    _root = root.GetComponent<RectTransform>();
    _root.SetParent(_holder, false);
    Drawing.Place(_root, 0, 0, Mathf.Max(1, settings.Width), Mathf.Max(1, settings.Height));
    _instance = instance;
    _update = instance.RunUpdate;
    instance.Bind(_root, _drawing, true);
    return _guard.Run(
      "starting",
      () =>
      {
        instance.RunLoad(data);
        instance.RunCreate();
        instance.RunSettingsChanged();
      }
    );
  }

  /// <summary>Once a frame: the mod updates what it drew. One that fails to update stays as it last drew.</summary>
  public void Tick(float frameScale)
  {
    if (_instance == null)
      return;
    if (!_guard.HasFailed("updating"))
      _guard.Run("updating", _update);
    _drawing.Tick(_unitsToFrame * frameScale);
  }

  /// <summary>
  /// The element at its in-game size, made smaller only to fit its area;
  /// false before there is a drawing. <paramref name="box"/> is its size on screen (frame units).
  /// </summary>
  public bool Fit(float frameScale, out Vector2 box)
  {
    box = default;
    if (_root == null)
      return false;
    Vector2 size = _root.sizeDelta;
    size = new Vector2(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    float units = _elementScale * OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height) / frameScale;
    float fit = Mathf.Min(1, _area.width / (size.x * units), _area.height / (size.y * units));
    _unitsToFrame = units * fit;
    box = size * _unitsToFrame;
    _boxTopLeft = new Vector2(_area.center.x - box.x * .5f, _area.center.y - box.y * .5f);
    float pixels = _unitsToFrame * frameScale;
    _holder.anchoredPosition = new Vector2(_boxTopLeft.x * frameScale, -_boxTopLeft.y * frameScale);
    _holder.sizeDelta = size;
    _holder.localScale = new Vector3(pixels, pixels, 1);
    return true;
  }

  /// <summary>While Grow from is open (<paramref name="show"/>), a dot at <paramref name="point"/> of the box.</summary>
  public void PlaceGrowMark(Vector2 box, bool show, Anchor9 point)
  {
    if (_growMark.gameObject.activeSelf != show)
      _growMark.gameObject.SetActive(show);
    if (!show)
      return;
    Vector2 at =
      _boxTopLeft
      + new Vector2(box.x * OverlayLayoutPosition.AnchorX(point), box.y * OverlayLayoutPosition.AnchorY(point));
    UiFactory.Place(_growMark.rectTransform, at.x - 6, at.y - 6, 12, 12);
    _growMark.transform.SetAsLastSibling();
  }

  /// <summary>The mod's instance goes; its drawing is cleared.</summary>
  public void TearDown()
  {
    if (_instance != null)
      _guard.Run("going away", _instance.RunDestroy);
    _instance = null;
    _update = null;
    _drawing?.Clear();
    _drawing = null;
    _root = null;
  }
}
