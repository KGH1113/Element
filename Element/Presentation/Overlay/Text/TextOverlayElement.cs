using System;
using System.Collections.Generic;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Overlay.Migration;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Overlay.Text;

/// <summary>
/// One TMP text built from runs. It is rebuilt only when a value it uses
/// changes, the highlight color or fonts change, or its runs are edited.
/// </summary>
internal sealed class TextOverlayElement : OverlayElementTarget
{
  private readonly OverlayValues _values;
  private readonly OverlayFonts _fonts;
  private readonly TextMeshProUGUI _text;
  private readonly OverlayContentGeometry _geometry;
  private readonly StringBuilder _builder = new(128);
  private readonly List<OverlayCompiledRun> _runs = new();
  private readonly OverlayTextPulse _pulse;
  private readonly OverlayTextGradients _gradients;
  private readonly List<(int Start, int Length)> _animated = new();
  private readonly OverlayShownValues _shown = new();
  private bool _preview;
  private bool _hasLegacy;
  private LegacyOverlayPlacement _legacy;
  private string _legacyKey;
  private int _themeVersion = -1;
  private int _fontVersion = -1;
  private float _renderedScale = -1;
  private bool _hasSizeFormula;

  // Counts up when TMP lays the text out or it is drawn again: the key
  // viewers behind it trace its glyphs again only then.
  private int _textVersion;
  private readonly Action<TMP_TextInfo> _laidOut;

  public TextOverlayElement(Transform parent, OverlayFonts fonts, OverlayValues values)
    : base(parent, "Text element")
  {
    _laidOut = _ => _textVersion++;
    _values = values;
    _fonts = fonts;
    _text = OverlayTextFactory.AddText(
      Rect.gameObject,
      fonts.DefaultAsset,
      fonts.DefaultMaterial,
      OverlayElementDefaults.TextSize,
      TextAlignmentOptions.Center
    );
    _text.textWrappingMode = TextWrappingModes.NoWrap;
    _text.overflowMode = TextOverflowModes.Overflow;
    _text.color = Color.white;
    _geometry = new OverlayContentGeometry(
      Rect,
      new[] { _text },
      new[] { OverlayElementDefaults.TextSize },
      new[] { Vector2.zero },
      new[] { Vector3.one },
      false,
      ContentSizeChanged
    );
    _pulse = Rect.gameObject.AddComponent<OverlayTextPulse>();
    _pulse.Initialize(_text);
    _gradients = Rect.gameObject.AddComponent<OverlayTextGradients>();
    _gradients.Initialize(_text);
    _text.OnPreRenderText += _laidOut;
  }

  public override string Label => L("텍스트", "Text");

  /// <summary>
  /// The words it shows now, without their look, cut short when long: what a
  /// list of elements calls it. "Text" while it shows nothing.
  /// </summary>
  public string Words
  {
    get
    {
      string rich = _text != null ? _text.text : null;
      if (string.IsNullOrEmpty(rich))
        return Label;
      System.Text.StringBuilder plain = new(rich.Length);
      OverlayPlainText.Append(plain, new System.Text.StringBuilder(rich));
      string words = plain.ToString().Trim();
      if (words.Length == 0)
        return Label;
      return words.Length > 32 ? words.Substring(0, 31) + "…" : words;
    }
  }

  /// <summary>The values this text shows.</summary>
  public OverlayValueMask ValueMask { get; private set; }

  public bool IsEmpty => Settings?.Text?.Runs == null || Settings.Text.Runs.Count == 0;

  // Selection, resizing and placement follow the drawn text, not the layout rectangle.
  protected override bool BoundsFollowContent => HasContent;

  internal OverlayFonts Fonts => _fonts;

  internal TextAlignmentOptions Alignment => _text.alignment;

  protected override bool TryGetLocalContent(out Rect local)
  {
    local = _geometry.Bounds;
    return _geometry.HasBounds;
  }

  protected override void ApplyContent()
  {
    Settings.Text ??= new OverlayTextSettings();
    string key = Settings.DefaultPlacement;
    if (key != _legacyKey)
    {
      _legacyKey = key;
      // Built-ins sit where they did in the bundled font, whatever the default font is now.
      _hasLegacy = !string.IsNullOrEmpty(key) && LegacyOverlayPlacement.TryGet(key, _fonts.Bundled, out _legacy);
      TextAlignmentOptions alignment = _hasLegacy ? _legacy.Alignment : TextAlignmentOptions.Center;
      if (_text.alignment != alignment)
        _text.alignment = alignment;
    }
    ValueMask = OverlayTextComposer.Compile(Settings.Text.Runs, _runs, _gradients);
    _hasSizeFormula = _runs.Exists(run => run.Size != null);
    // Edited runs start over: what they showed before is no change to grow on.
    _pulse.Stop();
    _shown.Clear();
    Render();
  }

  protected override void ResetPose()
  {
    float baseScale = BaseScale;
    if (_hasLegacy)
    {
      Vector2 size = _legacy.FitsText ? _text.GetPreferredValues() : _legacy.Size / baseScale;
      SetPose(Rect, _legacy.Anchor, _legacy.Pivot, _legacy.Position, size);
    }
    else
      SetPose(Rect, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
    Vector3 scale = new(baseScale, baseScale, 1);
    if (Rect.localScale != scale)
      Rect.localScale = scale;
  }

  private float BaseScale =>
    _hasLegacy && OverlayElementDefaults.TryGet(_legacyKey, out OverlayDefaultElement element) ? element.BaseScale : 1f;

  public override void SetPreview(bool preview)
  {
    if (_preview == preview)
      return;
    _preview = preview;
    // Examples and live values swapping places is no change to grow on.
    Render(true);
  }

  /// <summary>
  /// Rebuilds the text when one of <paramref name="changes"/> or the theme
  /// affects it. While <paramref name="settle"/> (a level starting or
  /// restarting), values take their new text without growing.
  /// </summary>
  public override void Refresh(OverlayValueMask changes, bool settle = false)
  {
    // Size formulas give pixels as drawn, so they follow the element's scale.
    if (
      changes.Overlaps(ValueMask)
      || _themeVersion != UiTheme.Version
      || _fontVersion != _fonts.Version
      || (_hasSizeFormula && !Mathf.Approximately(_renderedScale, EffectiveScale))
    )
      Render(settle);
  }

  public void Render() => Render(false);

  private void Render(bool settle)
  {
    if (Settings == null)
      return;
    _themeVersion = UiTheme.Version;
    _renderedScale = EffectiveScale;
    if (_fontVersion != _fonts.Version)
    {
      _fontVersion = _fonts.Version;
      // The material goes after the font, which resets it.
      if (_text.font != _fonts.DefaultAsset)
        _text.font = _fonts.DefaultAsset;
      if (_text.fontSharedMaterial != _fonts.DefaultMaterial)
        _text.fontSharedMaterial = _fonts.DefaultMaterial;
    }
    OverlayTextComposer.Compose(_builder, _runs, _values, _preview, UiTheme.Accent, _renderedScale, _fonts, _animated);
    _gradients.SetSource(_preview ? _values?.WithSamples : _values, UiTheme.Accent);
    string before = _text.text;
    _text.Assign(_builder);
    // A new text is colored as TMP lays it out; the same text is recolored now.
    if (ReferenceEquals(before, _text.text))
      _gradients.Repaint();
    // A gradient's colors alone can show or hide glyphs (fading to clear);
    // anything else that changes is laid out again (_laidOut).
    if (_gradients.Count > 0)
      _textVersion++;
    if (_hasLegacy && _legacy.FitsText)
    {
      // The combo's number and title were fitted to their text.
      Vector2 preferred = _text.GetPreferredValues();
      if (Rect.sizeDelta != preferred)
        Rect.sizeDelta = preferred;
    }
    PulseChanged(settle);
  }

  // An animated value that shows something new grows. The links in the text
  // are the animated values that show something, in order.
  private void PulseChanged(bool settle)
  {
    int link = 0;
    for (int slot = 0; slot < _animated.Count; slot++)
    {
      (int start, int length) = _animated[slot];
      if (_shown.Update(slot, _builder, start, length) && !settle && Enabled)
        _pulse.Play(link);
      if (length > 0)
        link++;
    }
  }

  // All three only count up, so the sum changes whenever one does. A copy of
  // an atlas arriving lets glyphs traced as boxes trace their shape.
  public override int CoverVersion => base.CoverVersion + _textVersion + _fonts.Atlases.Version;

  /// <summary>The glyph quads it shows, which hold its outline and shadow too.</summary>
  public override bool TryGetCoverBounds(out LayoutBounds screenTopLeft)
  {
    screenTopLeft = default;
    TMP_TextInfo info = OnScreen ? _text.textInfo : null;
    if (info == null)
      return false;
    Matrix4x4 toWorld = _text.rectTransform.localToWorldMatrix;
    float height = Screen.height;
    float left = float.PositiveInfinity,
      top = float.PositiveInfinity,
      right = float.NegativeInfinity,
      bottom = float.NegativeInfinity;
    int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
    for (int i = 0; i < count; i++)
    {
      ref readonly TMP_CharacterInfo character = ref info.characterInfo[i];
      if (!Shows(info, character))
        continue;
      Include(ToScreen(toWorld, character.vertex_BL.position, height), ref left, ref top, ref right, ref bottom);
      Include(ToScreen(toWorld, character.vertex_TL.position, height), ref left, ref top, ref right, ref bottom);
      Include(ToScreen(toWorld, character.vertex_TR.position, height), ref left, ref top, ref right, ref bottom);
      Include(ToScreen(toWorld, character.vertex_BR.position, height), ref left, ref top, ref right, ref bottom);
    }
    if (!(right > left && bottom > top))
      return false;
    screenTopLeft = new LayoutBounds(left, top, right - left, bottom - top);
    return true;
  }

  /// <summary>
  /// Each glyph as drawn at rest, with the overlay's outline and shadow: traced
  /// from its font atlas, or its quad while the atlas can't be read on the CPU.
  /// Fallback fonts' glyphs and gradient colors count like any others.
  /// </summary>
  public override void Rasterize(ViewerCoverRuns runs)
  {
    TMP_TextInfo info = OnScreen ? _text.textInfo : null;
    if (info == null)
      return;
    Matrix4x4 toWorld = _text.rectTransform.localToWorldMatrix;
    float height = Screen.height;
    FontAtlasPixels atlases = _fonts.Atlases;
    Material shaded = null;
    GlyphShading shading = default;
    int count = Mathf.Min(info.characterCount, info.characterInfo.Length);
    for (int i = 0; i < count; i++)
    {
      ref readonly TMP_CharacterInfo character = ref info.characterInfo[i];
      if (!Shows(info, character))
        continue;
      NVector2 bottomLeft = ToScreen(toWorld, character.vertex_BL.position, height),
        topLeft = ToScreen(toWorld, character.vertex_TL.position, height),
        topRight = ToScreen(toWorld, character.vertex_TR.position, height),
        bottomRight = ToScreen(toWorld, character.vertex_BR.position, height);
      Material material = MaterialOf(info, character);
      Texture texture = material != null ? material.mainTexture : null;
      if (
        character.elementType != TMP_TextElementType.Character
        || texture == null
        || !atlases.TryRead(texture, out FontAtlasAlpha atlas)
      )
      {
        runs.FillQuad(bottomLeft, topLeft, topRight, bottomRight);
        continue;
      }
      // Most glyphs share one material: read its shading once.
      if (!ReferenceEquals(material, shaded))
      {
        shaded = material;
        shading = GlyphShading.Of(material, atlas.Width, atlas.Height);
      }
      bool bold = (character.style & FontStyles.Bold) != 0 && !character.isUsingAlternateTypeface;
      GlyphCover.Trace(
        runs,
        bottomLeft,
        bottomRight,
        topLeft,
        character.vertex_BL.uv,
        character.vertex_TR.uv,
        atlas,
        shading,
        bold
      );
    }
  }

  // Visible glyphs whose color isn't fully transparent at every corner.
  private static bool Shows(TMP_TextInfo info, in TMP_CharacterInfo character)
  {
    if (!character.isVisible)
      return false;
    int mesh = character.materialReferenceIndex,
      vertex = character.vertexIndex;
    TMP_MeshInfo[] meshes = info.meshInfo;
    Color32[] colors = meshes != null && mesh >= 0 && mesh < meshes.Length ? meshes[mesh].colors32 : null;
    if (colors == null || vertex < 0 || vertex + 3 >= colors.Length)
      return character.color.a > 0;
    return colors[vertex].a > 0 || colors[vertex + 1].a > 0 || colors[vertex + 2].a > 0 || colors[vertex + 3].a > 0;
  }

  // The material its sub mesh draws with: the font's, or a fallback font's.
  private static Material MaterialOf(TMP_TextInfo info, in TMP_CharacterInfo character)
  {
    int mesh = character.materialReferenceIndex;
    TMP_MeshInfo[] meshes = info.meshInfo;
    Material material = meshes != null && mesh >= 0 && mesh < meshes.Length ? meshes[mesh].material : null;
    return material != null ? material : character.material;
  }

  private static void Include(NVector2 point, ref float left, ref float top, ref float right, ref float bottom)
  {
    left = Mathf.Min(left, point.X);
    top = Mathf.Min(top, point.Y);
    right = Mathf.Max(right, point.X);
    bottom = Mathf.Max(bottom, point.Y);
  }

  public override void Dispose()
  {
    if (_text != null)
      _text.OnPreRenderText -= _laidOut;
    _geometry.Dispose();
    base.Dispose();
  }
}
