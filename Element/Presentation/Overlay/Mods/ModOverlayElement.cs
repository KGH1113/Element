using System;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using ElementKit.Elements;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// An element another mod draws. Element places it like a text or a shape;
/// the mod measures and draws it from its properties. It is drawn again only
/// when a property, a value one uses, the highlight color or the mod's own
/// data changed. Without its mod, it keeps its place and properties: hidden
/// in the game, a labeled box in the editor that can still be moved or deleted.
/// </summary>
internal sealed class ModOverlayElement : OverlayElementTarget
{
  private const float MaximumSize = 4000;
  private static readonly Vector2 PlaceholderMinimum = new(240, 56);

  private readonly OverlayValues _values;
  private readonly ModElements _types;
  private readonly ModSurface _surface;
  private readonly ModProps _props = new();
  private ElementType _type;
  private int _typesVersion = -1;
  private int _typeRevision;
  private int _themeVersion = -1;
  private bool _preview;
  private bool _faulted;
  private Vector2 _size = PlaceholderMinimum;

  public ModOverlayElement(Transform parent, OverlayValues values, ModElements types, OverlayFonts fonts)
    : base(parent, "Mod element")
  {
    _values = values;
    _types = types;
    _surface = new ModSurface(Rect, fonts);
  }

  /// <summary>The type that draws this element; null while its mod is missing or off.</summary>
  public ElementType Type => _type;

  public override string Label =>
    _type != null ? L(_type.KoreanName, _type.EnglishName)
    : !string.IsNullOrEmpty(Settings?.Mod?.KoreanName) ? L(Settings.Mod.KoreanName, Settings.Mod.EnglishName)
    : TemplateId.Length > 0 ? "[" + TemplateId + "]"
    : L("모드 요소", "Mod element");

  /// <summary>What players read when they try to edit an element its mod can't draw now.</summary>
  public string Unavailable
  {
    get
    {
      string mod = OverlayElementKinds.ModOf(TemplateId);
      if (_faulted)
        return L(
          mod + " 모드에서 오류가 나서 이 요소를 멈췄어요. 모드를 업데이트하거나 만든 분께 알려 주세요.",
          mod + " stopped with an error, so this element is paused. Update the mod or tell its author."
        );
      return _types != null && _types.IsTurnedOff(TemplateId)
        ? L(
          mod + " 모드가 꺼져 있어요. UMM에서 켜면 다시 보여요.",
          mod + " is turned off. Turn it on in UMM to see this element."
        )
        : L(
          mod + " 모드가 있어야 보여요. 설치하면 그대로 돌아와요.",
          "This needs " + mod + ". Install it and the element comes back as it was."
        );
    }
  }

  /// <summary>The values this element's properties use.</summary>
  public OverlayValueMask ValueMask => _props.Mask;

  protected override bool TryGetLocalContent(out Rect local)
  {
    local = Rect.rect;
    return true;
  }

  protected override void ApplyContent()
  {
    Settings.Mod ??= new OverlayModSettings();
    _typesVersion = _types?.Version ?? -1;
    ElementType type = _types?.Find(TemplateId);
    if (!ReferenceEquals(type, _type))
    {
      _type = type;
      _faulted = false;
      _surface.State = null;
    }
    _props.Compile(_type, Settings.Mod);
    Redraw();
  }

  // Added in the middle; the editor places it from there.
  protected override void ResetPose()
  {
    Vector2 center = new(.5f, .5f);
    SetPose(Rect, center, center, Vector2.zero, _size);
    if (Rect.localScale != Vector3.one)
      Rect.localScale = Vector3.one;
  }

  public override void SetPreview(bool preview)
  {
    if (_preview == preview)
      return;
    _preview = preview;
    if (Settings != null)
      Redraw();
  }

  /// <summary>Once a frame: a few comparisons unless something it draws from changed.</summary>
  public void Refresh(OverlayValueMask changes)
  {
    if (Settings == null)
      return;
    if (_types != null && _types.Version != _typesVersion)
    {
      ApplyContent();
      return;
    }
    if (
      _themeVersion != OverlayTheme.Version
      || changes.Overlaps(_props.Mask)
      || (_type != null && _type.Revision != _typeRevision)
      || (_type != null && !_faulted && Enabled && _type.DrawsEveryFrame)
    )
      Redraw();
  }

  public void SetFonts(OverlayFonts fonts) => _surface.SetFonts(fonts);

  public void Redraw()
  {
    if (Settings == null || Rect == null)
      return;
    _themeVersion = OverlayTheme.Version;
    Vector2 size;
    if (_type == null || _faulted)
      size = DrawPlaceholder();
    else if (!TryDraw(out size))
    {
      _faulted = true;
      size = DrawPlaceholder();
    }
    // Remembered for when the mod is missing; not an edit.
    Settings.Mod.Width = size.x;
    Settings.Mod.Height = size.y;
    if (_type != null)
    {
      Settings.Mod.KoreanName = _type.KoreanName ?? string.Empty;
      Settings.Mod.EnglishName = _type.EnglishName ?? string.Empty;
    }
    if (_size == size)
      return;
    _size = size;
    Rect.sizeDelta = size;
    ContentSizeChanged();
  }

  private bool TryDraw(out Vector2 size)
  {
    _typeRevision = _type.Revision;
    IOverlayNumberSource source = _preview ? _values?.WithSamples : _values;
    _props.Use(source, _preview, OverlayTheme.Accent);
    size = _size;
    try
    {
      size = Valid(_type.RunMeasure(_props));
    }
    catch (Exception exception)
    {
      _types?.Failed(_type, "measuring itself", exception);
      return false;
    }
    if (Rect.sizeDelta != size)
      Rect.sizeDelta = size;
    _surface.Restore();
    _surface.Begin(size, PixelsPerUnit(size));
    try
    {
      _type.RunDraw(_surface, _props);
    }
    catch (Exception exception)
    {
      _surface.Clear();
      _types?.Failed(_type, "drawing", exception);
      return false;
    }
    _surface.End();
    return true;
  }

  // In the editor, a box with the reason where the element would be; in the
  // game, nothing. Either way it keeps the size it last drew at.
  private Vector2 DrawPlaceholder()
  {
    OverlayModSettings saved = Settings.Mod;
    Vector2 size = Valid(new Vector2(saved.Width, saved.Height));
    size = Vector2.Max(size, _preview ? PlaceholderMinimum : Vector2.one);
    _surface.Clear();
    if (!_preview)
      return size;
    _surface.Begin(size, PixelsPerUnit(size));
    _surface.Box(new Rect(Vector2.zero, size), ElementColor.From(new Color(1, 1, 1, .08f)), 10);
    string mod = OverlayElementKinds.ModOf(TemplateId);
    string line =
      _faulted ? L(mod + " 오류로 멈춤", mod + " stopped")
      : _types != null && _types.IsTurnedOff(TemplateId) ? L(mod + " 모드가 꺼져 있어요", mod + " is off")
      : L(mod + " 모드가 필요해요", "Needs " + mod);
    float fontSize = Mathf.Clamp(size.y * .32f, 14, 28);
    _surface.Text(
      new Rect(12, 0, size.x - 24, size.y),
      line,
      fontSize,
      ElementColor.From(new Color(1, 1, 1, .6f)),
      TextAlign.Center
    );
    _surface.End();
    return size;
  }

  // Pixels on screen (or in OrbitRender's video) per unit of the element.
  private float PixelsPerUnit(Vector2 size)
  {
    Rect bounds = CanvasRectBounds;
    return size.x > 0 && bounds.width > 0 ? bounds.width * CanvasScale / size.x : CanvasScale;
  }

  private static Vector2 Valid(Vector2 size) =>
    new(
      float.IsNaN(size.x) || float.IsInfinity(size.x) ? 1 : Mathf.Clamp(size.x, 1, MaximumSize),
      float.IsNaN(size.y) || float.IsInfinity(size.y) ? 1 : Mathf.Clamp(size.y, 1, MaximumSize)
    );

  public override void Dispose()
  {
    _surface.Dispose();
    base.Dispose();
  }
}
