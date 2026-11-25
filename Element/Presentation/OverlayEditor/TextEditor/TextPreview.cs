using System.Collections.Generic;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The text at its in-game size in the middle of the editor, made smaller
/// only to fit, as the game draws it with the editor's examples for values
/// that have no data yet. Each value is a link so its characters can be
/// found; gradient runs are painted across the whole text's box after
/// layout, as in the game.
/// </summary>
internal sealed class TextPreview
{
  private readonly TextDocumentEditing _text;
  private readonly OverlayValues _values;
  private readonly StringBuilder _builder = new(256);
  private readonly List<OverlayColorSettings> _composedGradients = new();
  private TextOverlayElement _element;
  private TextMeshProUGUI _preview;
  private OverlayTextPulse _previewPulse;
  private OverlayTextGradients _previewGradients;
  private float _previewScale = 1;
  private int _fontVersion = -1;

  public TextPreview(TextDocumentEditing text, OverlayValues values)
  {
    _text = text;
    _values = values;
  }

  public TextMeshProUGUI Text => _preview;

  /// <summary>Plays a value's growth on the preview.</summary>
  public OverlayTextPulse Pulse => _previewPulse;

  /// <summary>How much larger than in the game it is drawn.</summary>
  public float Shown { get; private set; } = 1;

  /// <summary>Screen pixels per unit of the text as it is shown.</summary>
  public float Scale => _previewScale;

  /// <summary>The values the preview's gradients use.</summary>
  public OverlayValueMask GradientMask => _previewGradients?.Mask ?? OverlayValueMask.None;

  /// <summary>A font finished loading since the preview took its fonts.</summary>
  public bool FontChanged => _element != null && _element.Fonts.Version != _fontVersion;

  /// <summary>Nothing typed or being composed; the hint shows instead.</summary>
  public bool Empty { get; private set; }

  /// <summary>The text's middle in screen pixels from the top left.</summary>
  public Vector2 Center { get; private set; }

  /// <summary>The text's top in frame units.</summary>
  public float Top { get; private set; }

  // The text is placed in screen pixels at its in-game size.
  public void Build(EditorFramePanel screen, TextOverlayElement element)
  {
    _element = element;
    RectTransform preview = screen.Ui.Rect("Text", screen.Stage, 0, 0, 10, 10);
    preview.pivot = new Vector2(.5f, .5f);
    _preview = OverlayTextFactory.AddText(
      preview.gameObject,
      element.Fonts.DefaultAsset,
      element.Fonts.DefaultMaterial,
      OverlayElementDefaults.TextSize,
      Horizontal(element.Alignment)
    );
    _fontVersion = element.Fonts.Version;
    _preview.textWrappingMode = TextWrappingModes.NoWrap;
    _preview.overflowMode = TextOverflowModes.Overflow;
    _previewPulse = preview.gameObject.AddComponent<OverlayTextPulse>();
    _previewPulse.Initialize(_preview);
    _previewGradients = preview.gameObject.AddComponent<OverlayTextGradients>();
    _previewGradients.Initialize(_preview);
  }

  /// <summary>
  /// Lays the text out at its game size, centered between
  /// <paramref name="left"/> and <paramref name="right"/> and between
  /// <paramref name="top"/> and <paramref name="bottom"/> (frame units of
  /// <paramref name="frameScale"/> screen pixels), smaller only to fit there.
  /// </summary>
  public void Draw(
    float left,
    float right,
    float top,
    float bottom,
    float frameScale,
    float elementScale,
    PreviewZoom zoom,
    bool editingColor,
    bool reduceMotion
  )
  {
    // The text must be active before it is laid out for the caret.
    Empty = _text.Document.Count == 0 && _text.Composition.Length == 0;
    if (_preview.gameObject.activeSelf == Empty)
      _preview.gameObject.SetActive(!Empty);
    Compose(elementScale);
    _preview.fontSize = OverlayElementDefaults.TextSize;
    _preview.text = _builder.ToString();
    Vector2 preferred = _preview.GetPreferredValues();
    preferred = new Vector2(Mathf.Max(2, preferred.x), Mathf.Max(_preview.fontSize, preferred.y));
    float middle = (left + right) * .5f;
    Vector2 available = new Vector2(Mathf.Max(40, right - left), Mathf.Max(40, bottom - top)) * frameScale;
    // The game's size (larger while a small text's color is edited), unless it would not fit.
    float game = elementScale * ScreenScale;
    float larger = zoom.For(
      editingColor,
      preferred.x * game / frameScale,
      preferred.y * game / frameScale,
      reduceMotion
    );
    _previewScale = Mathf.Min(game * larger, available.x / preferred.x, available.y / preferred.y);
    Shown = _previewScale / game;
    RectTransform rect = _preview.rectTransform;
    rect.sizeDelta = preferred;
    rect.localScale = new Vector3(_previewScale, _previewScale, 1);
    Center = new Vector2(middle, (top + bottom) * .5f) * frameScale;
    rect.anchoredPosition = new Vector2(Center.x, -Center.y);
    _preview.ForceMeshUpdate();
    Top = (Center.y - preferred.y * _previewScale * .5f) / frameScale;
  }

  /// <summary>Takes the element's default font again, once one finished loading.</summary>
  public void ApplyFont()
  {
    OverlayFonts fonts = _element.Fonts;
    _fontVersion = fonts.Version;
    if (_preview.font != fonts.DefaultAsset)
      _preview.font = fonts.DefaultAsset;
    if (_preview.fontSharedMaterial != fonts.DefaultMaterial)
      _preview.fontSharedMaterial = fonts.DefaultMaterial;
  }

  /// <summary>The box the text's gradients span, in its local space.</summary>
  public bool Box(out Rect box) => OverlayTextGradients.Box(_preview.textInfo, out box);

  /// <summary>The size of that box; one by one without characters.</summary>
  public Vector2 BoxSize => Box(out Rect box) ? box.size : Vector2.one;

  /// <summary>
  /// A point of the text (its local space) in <paramref name="frame"/>'s units
  /// from the top left, through the transforms alone, so it holds whatever
  /// camera draws the canvas.
  /// </summary>
  public Vector2 InFrame(RectTransform frame, Vector3 local)
  {
    Vector3 point = frame.InverseTransformPoint(_preview.rectTransform.TransformPoint(local));
    Rect bounds = frame.rect;
    return new Vector2(point.x - bounds.xMin, bounds.yMax - point.y);
  }

  // The text as the game will draw it, with the editor's examples for values
  // that have no data yet. Each value is a link so its characters can be found.
  private void Compose(float elementScale)
  {
    _builder.Clear();
    Color highlight = UiTheme.Accent;
    IOverlayNumberSource samples = _values.WithSamples;
    BeginComposeGradients(highlight);
    OverlayTextDocument document = _text.Document;
    string composition = _text.Composition;
    int caret = _text.Caret;
    OverlayTextItem? open = null;
    float openSize = 0;
    string openFont = null;
    for (int i = 0; i <= document.Count; i++)
    {
      if (i == caret && composition.Length > 0)
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize, openFont);
        open = null;
        float typing = OverlayTextComposer.Size(
          _text.TypingSize(i),
          OverlayFormulaText.Compile(_text.TypingSizeFormula(i)),
          samples,
          elementScale
        );
        string typingFont = FontTag(_text.TypingFont(i));
        OverlayTextComposer.OpenStyle(
          _builder,
          PaintColor(_text.TypingColor(i), highlight, samples),
          typing,
          typingFont
        );
        _builder.Append("<u>");
        OverlayTextComposer.AppendLiteral(_builder, composition);
        _builder.Append("</u>");
        OverlayTextComposer.CloseStyle(_builder, typing, typingFont);
      }
      if (i == document.Count)
        break;
      OverlayTextItem item = document[i];
      if (item.IsValue)
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize, openFont);
        open = null;
        float size = ItemSize(item, samples, elementScale);
        string font = FontTag(item.Font);
        OverlayTextComposer.OpenStyle(_builder, PaintColor(item.Color, highlight, samples), size, font);
        _builder.Append("<link=\"").Append(OverlayTextComposer.ValueLink).Append("\">");
        int before = _builder.Length;
        OverlayFormulaText.Append(_builder, OverlayVariable.Parse(item.Value), _values, true);
        if (_builder.Length == before)
          _builder.Append(' ');
        _builder.Append("</link>");
        OverlayTextComposer.CloseStyle(_builder, size, font);
        continue;
      }
      if (open == null || !open.Value.SameStyle(item))
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize, openFont);
        openSize = ItemSize(item, samples, elementScale);
        openFont = FontTag(item.Font);
        OverlayTextComposer.OpenStyle(_builder, PaintColor(item.Color, highlight, samples), openSize, openFont);
        open = item;
      }
      OverlayTextComposer.AppendLiteral(_builder, item.Text);
    }
    if (open != null)
      OverlayTextComposer.CloseStyle(_builder, openSize, openFont);
  }

  // A run in another font than the default switches to it, as in the game.
  private string FontTag(string font) => _element?.Fonts?.OpenTag(font);

  private static float ItemSize(OverlayTextItem item, IOverlayNumberSource samples, float elementScale) =>
    OverlayTextComposer.Size(item.Size, OverlayFormulaText.Compile(item.SizeFormula), samples, elementScale);

  // Gradient runs are written in a marker color the preview paints after
  // layout; one slot per distinct gradient.
  private void BeginComposeGradients(Color highlight)
  {
    _composedGradients.Clear();
    _previewGradients.Clear();
    _previewGradients.SetSource(_values.WithSamples, highlight);
  }

  private Color PaintColor(OverlayColorSettings color, Color highlight, IOverlayNumberSource samples)
  {
    // A case that holds gives a solid color, over a gradient too.
    OverlayVariable cases = OverlayTextComposer.Cases(color);
    Color solid = OverlayTextComposer.Resolve(
      color,
      highlight,
      OverlayFormulaText.Compile(color?.AlphaFormula),
      samples,
      cases,
      out bool cased
    );
    if (color != null && color.IsGradient && !cased)
    {
      for (int i = 0; i < _composedGradients.Count; i++)
        if (_composedGradients[i].SameAs(color))
          return OverlayTextGradients.Marker(i);
      _composedGradients.Add(color);
      return OverlayTextGradients.Marker(_previewGradients.Add(color));
    }
    return OverlayTextGradients.AvoidMarker(solid);
  }

  private static TextAlignmentOptions Horizontal(TextAlignmentOptions alignment)
  {
    int value = (int)alignment & 0xFF;
    return value switch
    {
      (int)HorizontalAlignmentOptions.Left => TextAlignmentOptions.MidlineLeft,
      (int)HorizontalAlignmentOptions.Right => TextAlignmentOptions.MidlineRight,
      _ => TextAlignmentOptions.Midline,
    };
  }

  // Screen pixels per reference unit of the overlay canvas.
  private static float ScreenScale => OverlayLayoutPosition.ScreenScale(Screen.width, Screen.height);
}
