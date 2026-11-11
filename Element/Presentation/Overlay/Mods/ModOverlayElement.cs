using System;
using System.Collections.Generic;
using Element.Application.Mods;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Ui.Widgets;
using ElementKit;
using ElementKit.Hosting;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// An element another mod draws. Element places it like a text or a shape;
/// the mod's own <see cref="OverlayElement"/> draws inside it, from the
/// settings Element keeps for it. Without its mod it keeps its place, size and
/// settings: hidden in the game, a labeled box in the editor that can still be
/// moved or deleted. A mod's failure pauses only its element.
/// </summary>
internal sealed class ModOverlayElement : OverlayElementTarget
{
  private const float MaximumSize = 4000;
  private static readonly Vector2 PlaceholderMinimum = new(240, 56);
  private static readonly Vector2 StartSize = new(100, 100);

  private readonly ModElements _types;
  private readonly ModDrawing _drawing;
  private OverlayFonts _fonts;
  private ModDrawing _placeholderDrawing;
  private RectTransform _placeholder;
  private string _placeholderLine;
  private ElementClass _class;
  private OverlayElement _instance;
  private RectTransform _root;
  private string _loaded;
  private int _typesVersion = -1;
  private bool _preview;
  private bool _faulted;
  private Vector2 _size = PlaceholderMinimum;

  public ModOverlayElement(Transform parent, ModElements types, OverlayFonts fonts)
    : base(parent, "Mod element")
  {
    _types = types;
    _fonts = fonts;
    _drawing = new ModDrawing(fonts);
  }

  /// <summary>The kind that draws this element; null while its mod is missing or off.</summary>
  public ElementClass Class => _class;

  /// <summary>The fonts its mod draws with, so Element keeps them loaded.</summary>
  public IEnumerable<string> UsedFonts => _drawing.UsedFonts;

  /// <summary>Whether its mod draws it right now.</summary>
  public bool Drawn => _instance != null && !_faulted;

  public override string Label =>
    _class != null ? L(_class.Info.KoreanName, _class.Info.EnglishName)
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

  protected override bool TryGetLocalContent(out Rect local)
  {
    local = Rect.rect;
    return true;
  }

  protected override void ApplyContent()
  {
    Settings.Mod ??= new OverlayModSettings();
    _typesVersion = _types?.Version ?? -1;
    ElementClass type = _types?.Find(TemplateId);
    if (!ReferenceEquals(type, _class))
    {
      DestroyInstance();
      _class = type;
      _faulted = false;
      if (_class != null)
        CreateInstance();
    }
    else if (_instance != null && !_faulted && _loaded != (Settings.Mod.Data ?? string.Empty))
      Reload();
    if (_class != null)
    {
      Settings.Mod.KoreanName = _class.Info.KoreanName ?? string.Empty;
      Settings.Mod.EnglishName = _class.Info.EnglishName ?? string.Empty;
    }
    FollowSize();
    ShowPlaceholder();
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
    _instance?.SetPreview(preview);
    if (Settings == null)
      return;
    FollowSize();
    ShowPlaceholder();
  }

  public void SetFonts(OverlayFonts fonts)
  {
    _fonts = fonts;
    _drawing.SetFonts(fonts);
    _placeholderDrawing?.SetFonts(fonts);
  }

  /// <summary>Once a frame while the overlay is shown: the mod updates what it drew, Element follows its size.</summary>
  public override void Refresh(OverlayValueMask changes, bool settle)
  {
    if (Settings == null)
      return;
    if (_types != null && _types.Version != _typesVersion)
    {
      ApplyContent();
      return;
    }
    if (_instance == null || _faulted || !Enabled)
      return;
    try
    {
      _instance.RunUpdate();
    }
    catch (Exception exception)
    {
      Fault("updating", exception);
      return;
    }
    _drawing.Tick(PixelsPerUnit);
    FollowSize();
  }

  private void CreateInstance()
  {
    OverlayElement instance = _class.Create(out Exception error);
    if (instance == null)
    {
      _faulted = true;
      _types?.Failed(_class, "being made", error);
      return;
    }
    GameObject root = new("Mod drawing", typeof(RectTransform));
    _root = root.GetComponent<RectTransform>();
    _root.SetParent(Rect, false);
    ElementKit.Drawing.Place(_root, 0, 0, Saved.x, Saved.y);
    _instance = instance;
    instance.Bind(_root, _drawing, _preview);
    _loaded = Settings.Mod.Data ?? string.Empty;
    try
    {
      instance.RunLoad(_loaded);
      instance.RunCreate();
      instance.RunSettingsChanged();
    }
    catch (Exception exception)
    {
      Fault("starting", exception);
    }
  }

  // The player changed the settings: undo, Cancel or the editing screen's Done.
  private void Reload()
  {
    _loaded = Settings.Mod.Data ?? string.Empty;
    try
    {
      _instance.RunLoad(_loaded);
      _instance.RunSettingsChanged();
    }
    catch (Exception exception)
    {
      Fault("loading its settings", exception);
    }
  }

  private void Fault(string doing, Exception exception)
  {
    _faulted = true;
    _types?.Failed(_class, doing, exception);
    if (_root != null)
      _root.gameObject.SetActive(false);
    FollowSize();
    ShowPlaceholder();
  }

  private void DestroyInstance()
  {
    if (_instance != null)
      try
      {
        _instance.RunDestroy();
      }
      catch (Exception exception)
      {
        _types?.Failed(_class, "going away", exception);
      }
    _instance = null;
    _drawing.Clear();
    if (_root != null)
      UiFactory.Destroy(_root.gameObject);
    _root = null;
    _loaded = null;
  }

  // The size the mod drew at; without its mod, the saved one, or room for the reason.
  private void FollowSize()
  {
    Vector2 size = Drawn ? Valid(_root.sizeDelta) : Vector2.Max(Saved, _preview ? PlaceholderMinimum : Vector2.one);
    if (Drawn)
    {
      Settings.Mod.Width = size.x;
      Settings.Mod.Height = size.y;
    }
    if (_size == size)
      return;
    _size = size;
    Rect.sizeDelta = size;
    ContentSizeChanged();
  }

  private Vector2 Saved =>
    Settings?.Mod != null && Settings.Mod.Width >= 1 && Settings.Mod.Height >= 1
      ? Valid(new Vector2(Settings.Mod.Width, Settings.Mod.Height))
      : StartSize;

  // In the editor, a box saying why the element is empty; in the game, nothing.
  private void ShowPlaceholder()
  {
    string line = _preview && !Drawn ? PlaceholderLine : null;
    if (line == null)
    {
      if (_placeholder != null && _placeholder.gameObject.activeSelf)
        _placeholder.gameObject.SetActive(false);
      return;
    }
    if (_placeholder == null)
    {
      GameObject holder = new("Placeholder", typeof(RectTransform));
      _placeholder = holder.GetComponent<RectTransform>();
      _placeholder.SetParent(Rect, false);
      _placeholderDrawing = new ModDrawing(_fonts);
    }
    _placeholder.gameObject.SetActive(true);
    if (line == _placeholderLine && _placeholder.rect.size == _size)
      return;
    _placeholderLine = line;
    for (int i = _placeholder.childCount - 1; i >= 0; i--)
      UiFactory.Destroy(_placeholder.GetChild(i).gameObject);
    _placeholderDrawing.Clear();
    ElementKit.Drawing.Place(_placeholder, 0, 0, _size.x, _size.y);
    RoundedBox box = _placeholderDrawing.Box(_placeholder);
    ElementKit.Drawing.Place(box.Rect, 0, 0, _size.x, _size.y);
    box.Color = new Color(1, 1, 1, .08f);
    box.Rounding = 10;
    TextMeshProUGUI text = _placeholderDrawing.Text(_placeholder, line, Mathf.Clamp(_size.y * .32f, 14, 28));
    ElementKit.Drawing.Place(text.rectTransform, 12, 0, _size.x - 24, _size.y);
    text.alignment = TextAlignmentOptions.Center;
    text.color = new Color(1, 1, 1, .6f);
    _placeholderDrawing.Tick(PixelsPerUnit);
  }

  private string PlaceholderLine
  {
    get
    {
      string mod = OverlayElementKinds.ModOf(TemplateId);
      return _faulted ? L(mod + " 오류로 멈춤", mod + " stopped")
        : _types != null && _types.IsTurnedOff(TemplateId) ? L(mod + " 모드가 꺼져 있어요", mod + " is off")
        : L(mod + " 모드가 필요해요", "Needs " + mod);
    }
  }

  // Pixels on screen (or in OrbitRender's video) per unit of the element.
  private float PixelsPerUnit
  {
    get
    {
      Rect bounds = CanvasRectBounds;
      return _size.x > 0 && bounds.width > 0 ? bounds.width * CanvasScale / _size.x : CanvasScale;
    }
  }

  private static Vector2 Valid(Vector2 size) =>
    new(
      float.IsNaN(size.x) || float.IsInfinity(size.x) ? 1 : Mathf.Clamp(size.x, 1, MaximumSize),
      float.IsNaN(size.y) || float.IsInfinity(size.y) ? 1 : Mathf.Clamp(size.y, 1, MaximumSize)
    );

  public override void Dispose()
  {
    DestroyInstance();
    base.Dispose();
  }
}
