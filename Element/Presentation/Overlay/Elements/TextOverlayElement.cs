using System.Collections.Generic;
using System.Text;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Layout;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Overlay.Elements;

/// <summary>
/// One TMP text built from runs. It is rebuilt only when a value it uses
/// changes, the highlight color changes, or its runs are edited.
/// </summary>
internal sealed class TextOverlayElement : OverlayElementTarget
{
  private readonly OverlayValues _values;
  private readonly TMP_FontAsset _font;
  private readonly TextMeshProUGUI _text;
  private readonly OverlayContentGeometry _geometry;
  private readonly StringBuilder _builder = new(128);
  private readonly List<OverlayCompiledRun> _runs = new();
  private readonly OverlayTextPulse _pulse;
  private bool _preview;
  private bool _hasLegacy;
  private LegacyOverlayPlacement _legacy;
  private string _legacyKey;
  private int _themeVersion = -1;
  private float _renderedScale = -1;
  private bool _hasSizeFormula;

  public TextOverlayElement(Transform parent, TMP_FontAsset font, Material material, OverlayValues values)
    : base(parent, "Text element")
  {
    _values = values;
    _font = font;
    _text = OverlayTextFactory.AddText(
      Rect.gameObject,
      font,
      material,
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
  }

  public override string Label => L("텍스트", "Text");

  /// <summary>The values this text shows, as OverlayValueCatalog bits.</summary>
  public ulong ValueMask { get; private set; }

  public bool UsesCombo => (ValueMask & OverlayValueCatalog.Bit(OverlayValueId.Combo)) != 0;

  public bool IsEmpty => Settings?.Text?.Runs == null || Settings.Text.Runs.Count == 0;

  // Selection and resizing follow the drawn text, not the layout rectangle.
  public override Rect Bounds => HasContent ? AnchorBounds : RectBounds;

  internal TMP_FontAsset Font => _font;

  internal Material Material => _text.fontSharedMaterial;

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
      _hasLegacy = !string.IsNullOrEmpty(key) && LegacyOverlayPlacement.TryGet(key, _font, out _legacy);
      TextAlignmentOptions alignment = _hasLegacy ? _legacy.Alignment : TextAlignmentOptions.Center;
      if (_text.alignment != alignment)
        _text.alignment = alignment;
    }
    ValueMask = OverlayTextComposer.Compile(Settings.Text.Runs, _runs);
    _hasSizeFormula = _runs.Exists(run => run.Size != null);
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
    Render();
  }

  /// <summary>Rebuilds the text when one of <paramref name="changes"/> or the theme affects it.</summary>
  public void Refresh(ulong changes)
  {
    // Size formulas give pixels as drawn, so they follow the element's scale.
    if (
      (changes & ValueMask) != 0
      || _themeVersion != OverlayTheme.Version
      || (_hasSizeFormula && !Mathf.Approximately(_renderedScale, EffectiveScale))
    )
      Render();
  }

  public void Pulse() => _pulse.Play();

  public void Render()
  {
    if (Settings == null)
      return;
    _themeVersion = OverlayTheme.Version;
    _renderedScale = EffectiveScale;
    OverlayTextComposer.Compose(_builder, _runs, _values, _preview, OverlayTheme.Accent, _renderedScale);
    _text.Assign(_builder);
    if (_hasLegacy && _legacy.FitsText)
    {
      // The combo's number and title were fitted to their text.
      Vector2 preferred = _text.GetPreferredValues();
      if (Rect.sizeDelta != preferred)
        Rect.sizeDelta = preferred;
    }
  }

  public override void Dispose()
  {
    _geometry.Dispose();
    base.Dispose();
  }
}
