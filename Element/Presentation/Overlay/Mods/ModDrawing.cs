using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Ui.Theme;
using ElementKit;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// Element's drawing helpers for one mod element: texts in the overlay's
/// fonts, which follow a newly picked or newly loaded font, and boxes drawn as
/// Element's own shapes, gradients and the highlight color included, with
/// edges kept one pixel soft at any scale.
/// </summary>
internal sealed class ModDrawing : Drawing
{
  private readonly List<TextMeshProUGUI> _texts = new();
  private readonly Dictionary<TextMeshProUGUI, string> _fontOf = new();
  private readonly List<ModBox> _boxes = new();
  private OverlayFonts _fonts;
  private int _fontVersion = -1;
  private int _themeVersion = -1;
  private float _pixelsPerUnit = 1;

  public ModDrawing(OverlayFonts fonts) => _fonts = fonts;

  public override Color Highlight => UiTheme.Accent;

  /// <summary>The fonts this element's texts were set to, so Element keeps them loaded.</summary>
  public IEnumerable<string> UsedFonts => _fontOf.Values;

  public void SetFonts(OverlayFonts fonts)
  {
    _fonts = fonts;
    _fontVersion = -1;
  }

  public override TextMeshProUGUI Text(Transform parent, string text = "", float size = 24)
  {
    GameObject child = new("Text", typeof(RectTransform));
    RectTransform rect = child.GetComponent<RectTransform>();
    rect.SetParent(parent, false);
    Place(rect, 0, 0, 200, Mathf.Max(1, size * 1.4f));
    TextMeshProUGUI label = OverlayTextFactory.AddText(
      child,
      _fonts?.DefaultAsset,
      _fonts?.DefaultMaterial,
      size,
      TextAlignmentOptions.TopLeft
    );
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.overflowMode = TextOverflowModes.Overflow;
    label.text = text ?? string.Empty;
    _texts.Add(label);
    return label;
  }

  public override void SetFont(TextMeshProUGUI text, string font)
  {
    if (text == null)
      return;
    font = OverlayFontKey.Normalize(font);
    if (font.Length == 0)
      _fontOf.Remove(text);
    else
      _fontOf[text] = font;
    if (!_texts.Contains(text))
      _texts.Add(text);
    ApplyFont(text);
  }

  public override RoundedBox Box(Transform parent)
  {
    ModBox box = new(parent, _pixelsPerUnit);
    _boxes.Add(box);
    return box;
  }

  /// <summary>
  /// Once a frame: texts take a newly picked or loaded font, boxes that follow
  /// the highlight take a new one, and all take the screen's pixels; a few
  /// comparisons when nothing changed. Destroyed ones are let go.
  /// </summary>
  public void Tick(float pixelsPerUnit)
  {
    int fonts = _fonts?.Version ?? 0;
    bool fontChanged = fonts != _fontVersion;
    _fontVersion = fonts;
    for (int i = _texts.Count - 1; i >= 0; i--)
    {
      TextMeshProUGUI text = _texts[i];
      if (text == null)
      {
        _texts.RemoveAt(i);
        continue;
      }
      if (fontChanged)
        ApplyFont(text);
    }
    if (_fontOf.Count > 0 && fontChanged)
      DropDestroyedFonts();
    bool themeChanged = _themeVersion != UiTheme.Version;
    _themeVersion = UiTheme.Version;
    bool pixelsChanged = pixelsPerUnit > 0 && Mathf.Abs(pixelsPerUnit - _pixelsPerUnit) > .01f;
    if (pixelsChanged)
      _pixelsPerUnit = pixelsPerUnit;
    for (int i = _boxes.Count - 1; i >= 0; i--)
    {
      ModBox box = _boxes[i];
      if (!box.Alive)
      {
        _boxes.RemoveAt(i);
        continue;
      }
      if (pixelsChanged)
        box.SetPixelsPerUnit(_pixelsPerUnit);
      if (themeChanged)
        box.HighlightChanged();
    }
  }

  /// <summary>Forgets what it made; the objects go with their element.</summary>
  public void Clear()
  {
    _texts.Clear();
    _fontOf.Clear();
    _boxes.Clear();
  }

  // A chosen font when it is ready; the overlay's default until then.
  private void ApplyFont(TextMeshProUGUI text)
  {
    if (_fonts == null)
      return;
    TMP_FontAsset asset = _fonts.DefaultAsset;
    Material material = _fonts.DefaultMaterial;
    if (
      _fontOf.TryGetValue(text, out string key)
      && _fonts.TryFont(key, out TMP_FontAsset chosen, out Material chosenMaterial)
    )
    {
      asset = chosen;
      material = chosenMaterial;
    }
    if (text.font != asset)
      text.font = asset;
    if (text.fontSharedMaterial != material)
      text.fontSharedMaterial = material;
  }

  private void DropDestroyedFonts()
  {
    List<TextMeshProUGUI> gone = null;
    foreach (TextMeshProUGUI text in _fontOf.Keys)
      if (text == null)
        (gone ??= new List<TextMeshProUGUI>()).Add(text);
    if (gone != null)
      foreach (TextMeshProUGUI text in gone)
        _fontOf.Remove(text);
  }
}

/// <summary>A rounded box drawn as an Element square that follows its UI object's size at once.</summary>
internal sealed class ModBox : RoundedBox
{
  private readonly ShapeGraphic _graphic;
  private readonly OverlayShapeSettings _shape = OverlayShapeGeometry.Create();
  private readonly OverlayGradientFormulas _formulas = new();
  private readonly OverlayResolvedGradient _gradient = new();
  private Color _color = Color.white;
  private Paint _paint;
  private OverlayColorSettings _paintSettings;

  public ModBox(Transform parent, float pixelsPerUnit)
  {
    GameObject child = new("Box", typeof(RectTransform));
    Rect = child.GetComponent<RectTransform>();
    Rect.SetParent(parent, false);
    Drawing.Place(Rect, 0, 0, 100, 100);
    _graphic = child.AddComponent<ShapeGraphic>();
    _graphic.raycastTarget = false;
    _graphic.FitsRect = true;
    _shape.StrokeWidth = 0;
    _graphic.SetPixelsPerUnit(pixelsPerUnit);
    _graphic.SetShape(_shape, _color, Color.clear);
  }

  public override RectTransform Rect { get; }

  public bool Alive => _graphic != null;

  public override Color Color
  {
    get => _color;
    set
    {
      if (_paint == null && _color == value)
        return;
      _paint = null;
      _paintSettings = null;
      _color = value;
      _graphic.SetShape(_shape, _color, Color.clear);
    }
  }

  public override Paint Paint
  {
    get => _paint?.Clone();
    set
    {
      _paint = value?.Clone();
      _paintSettings = _paint == null ? null : ModPaints.ToSettings(_paint);
      if (_paintSettings != null)
        _formulas.Compile(_paintSettings);
      DrawPaint();
    }
  }

  public override float Rounding
  {
    get => _graphic.FitRounding;
    set
    {
      value = float.IsNaN(value) ? 0 : Mathf.Max(0, value);
      if (Mathf.Approximately(_graphic.FitRounding, value))
        return;
      _graphic.FitRounding = value;
      _graphic.SetVerticesDirty();
    }
  }

  public override bool Visible
  {
    get => Rect != null && Rect.gameObject.activeSelf;
    set
    {
      if (Rect != null && Rect.gameObject.activeSelf != value)
        Rect.gameObject.SetActive(value);
    }
  }

  /// <summary>Keeps edges one screen pixel soft at a new scale.</summary>
  public void SetPixelsPerUnit(float pixelsPerUnit) => _graphic.SetPixelsPerUnit(pixelsPerUnit);

  /// <summary>Players picked another highlight color; a paint that follows it is drawn again.</summary>
  public void HighlightChanged()
  {
    if (_paint != null && _paint.Highlight)
      DrawPaint();
  }

  private void DrawPaint()
  {
    if (_paintSettings == null)
    {
      _graphic.SetShape(_shape, _color, Color.clear);
      return;
    }
    _color = OverlayTextComposer.Resolve(_paintSettings, UiTheme.Accent);
    Vector2 size = Rect.rect.size;
    bool gradient = _formulas.Resolve(
      _paintSettings,
      UiTheme.Accent,
      null,
      Mathf.Max(1, size.x),
      Mathf.Max(1, size.y),
      _gradient
    );
    _graphic.SetShape(_shape, _color, Color.clear, gradient ? _gradient : null, null);
  }
}
