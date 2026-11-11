using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Text;
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
