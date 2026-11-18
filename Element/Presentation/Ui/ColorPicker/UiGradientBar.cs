using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Gradient;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Ui.ColorPicker;

// The color bar on top of an overlay editor's picker. One color is solid;
// clicking the bar adds a color and makes a gradient. Its squares move along
// the bar, the picked one shows a delete button above it, and the square, hue
// and opacity controls beside it edit the picked color; an editor shows its
// position as a number (PositionSlot). Four tiles drawn in the current colors pick the
// kind, beside flip and a quarter turn. Following the highlight, only
// opacities differ. Its parts sit in Root, which the picker's screen shows
// where the bar goes; the tip over the bar floats on the picker itself.
internal sealed class UiGradientBar : IGradientBar
{
  private const float BarHeight = 26,
    BarInset = 11,
    StopSize = 18,
    LabelRow = 16,
    DeleteRow = 30,
    BarTop = LabelRow + DeleteRow,
    KindRow = 34,
    GradientGap = 10,
    DeleteSize = 28,
    TipHeight = 26,
    TipGap = 4;

  private readonly UiFactory _ui;
  private readonly IGradientBarHost _host;
  private readonly Color _under;
  private readonly RectTransform _bar,
    _kindRow,
    _positionSlot,
    _tip;
  private readonly UiGradientPreview _barPreview;
  private readonly List<UiSwatchGraphic> _barStops = new();
  private readonly Image[] _kindFrames = new Image[4];
  private readonly UiGradientPreview[] _kindPreviews = new UiGradientPreview[4];
  private readonly Button _flip,
    _rotate,
    _delete;
  private readonly TextMeshProUGUI _tipText;
  private OverlayColorSettings _working;
  private float _column;
  private int _stop;
  private int _dragging = -1;
  private float _grab;
  private readonly OverlayResolvedGradient _previewGradient = new();

  /// <summary>Another color was picked on the bar or by the editor.</summary>
  public Action StopSelected;

  /// <summary>The element's box in units, for turning the gradient; a square when unset.</summary>
  public Func<Vector2> GradientBox;

  /// <summary>Where a stop shows and how opaque, with its formula worked out; its plain value when unset.</summary>
  public Func<int, float> ShownPosition,
    ShownAlpha;

  /// <param name="parent">Where <see cref="Root"/> waits until a screen shows it.</param>
  /// <param name="tipLayer">What the tip over the bar floats on, in front of the picker's other controls.</param>
  /// <param name="under">What the bar is drawn on: its buttons are opaque, a little lighter than that.</param>
  public UiGradientBar(UiFactory ui, Transform parent, RectTransform tipLayer, IGradientBarHost host, Color under)
  {
    _ui = ui;
    _host = host;
    _under = under;
    Root = _ui.Rect("Gradient", parent, 0, 0, 256, Height);
    RectTransform root = Root;
    _bar = _ui.Rect("Color bar", root, 0, 0, 256, BarHeight);
    UiCapsuleGraphic track = _ui.Capsule("Color bar track", _bar, 0, 0, 256, BarHeight, Color.white);
    track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    UiFactory.Stretch(track.rectTransform, 0, 0, 0, 0);
    _barPreview = _ui.Rect("Colors", track.transform, 0, 0, 256, BarHeight)
      .gameObject.AddComponent<UiGradientPreview>();
    UiFactory.Stretch(_barPreview.rectTransform, 0, 0, 0, 0);
    _barPreview.raycastTarget = false;
    _barPreview.AlongX = true;
    _barPreview.Inset = BarInset;
    UiPointerArea area = _ui.Rect("Color bar input", _bar, 0, -4, 256, BarHeight + 8)
      .gameObject.AddComponent<UiPointerArea>();
    area.raycastTarget = true;
    area.Down = BarDown;
    area.Dragged = BarDrag;
    area.Up = _ => _dragging = -1;
    area.Moved = BarHover;
    area.Exited = () => _tip.gameObject.SetActive(false);
    UiCursor.Attach(area.gameObject, UiCursorKind.Pointer);

    _kindRow = _ui.Rect("Gradient kinds", root, 0, 0, 256, KindRow);
    string[] names = { L("선형", "Linear"), L("방사형", "Radial"), L("회전형", "Angular"), L("다이아몬드", "Diamond") };
    for (int i = 0; i < 4; i++)
    {
      OverlayGradientKind kind = (OverlayGradientKind)i;
      Image frame = _ui.Panel(names[i], _kindRow, 0, 0, 38, KindRow, new Color(.12f, .12f, .12f));
      frame.raycastTarget = true;
      Button button = frame.gameObject.AddComponent<Button>();
      button.transition = Selectable.Transition.None;
      button.navigation = new Navigation { mode = Navigation.Mode.None };
      button.onClick.AddListener(() => SetKind(kind));
      UiCursor.Attach(frame.gameObject, UiCursorKind.Pointer);
      Image mask = _ui.Panel("Mask", frame.transform, 3, 3, 32, KindRow - 6, Color.white);
      mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      UiFactory.Stretch(mask.rectTransform, 3, 3, 3, 3);
      UiGradientPreview preview = _ui.Rect("Preview", mask.transform, 0, 0, 32, KindRow - 6)
        .gameObject.AddComponent<UiGradientPreview>();
      UiFactory.Stretch(preview.rectTransform, 0, 0, 0, 0);
      preview.raycastTarget = false;
      preview.Cell = 4;
      _kindFrames[i] = frame;
      _kindPreviews[i] = preview;
    }
    _flip = IconButton(L("뒤집기", "Flip"), _kindRow, UiIconKind.Flip, Flip, KindRow);
    _rotate = IconButton(L("90° 돌리기", "Turn 90°"), _kindRow, UiIconKind.Rotate, Rotate, KindRow);

    _ui.Text(L("색 배치", "Colors"), root, 0, 0, 200, LabelRow, 12, UiPalette.Muted).raycastTarget = false;
    _positionSlot = _ui.Rect("Position slot", parent, 0, 0, 10, 40);
    _delete = IconButton(
      L("이 색 지우기", "Remove this color"),
      root,
      UiIconKind.RemoveStop,
      RemoveSelectedStop,
      DeleteSize
    );

    UiCapsuleGraphic tip = _ui.Capsule("Add color tip", tipLayer, 0, 0, 10, TipHeight, new Color(.11f, .11f, .11f));
    _tip = tip.rectTransform;
    _tipText = _ui.Text(L("눌러서 색 추가", "Click to add a color"), _tip, 0, 0, 10, TipHeight, 12);
    _tipText.alignment = TextAlignmentOptions.Midline;
    _tipText.textWrappingMode = TextWrappingModes.NoWrap;
    UiFactory.Stretch(_tipText.rectTransform, 0, 0, 0, 0);
    _tip.gameObject.SetActive(false);
  }

  /// <summary>The bar with its pill row and kinds, top left at the bar's place in the picker.</summary>
  public RectTransform Root { get; }

  public bool IsGradient => _host.GradientShown && _working != null && _working.IsGradient;

  /// <summary>The picked stop's index in the gradient's stops; -1 for a solid color.</summary>
  public int SelectedStop => IsGradient ? _stop : -1;

  /// <summary>Where the owner shows the picked color's position as a number.</summary>
  public RectTransform PositionSlot => _positionSlot;

  /// <inheritdoc />
  public bool HasPosition { get; set; }

  private const float SolidHeight = BarTop + BarHeight,
    GradientHeight = BarTop + BarHeight + GradientGap + KindRow;

  /// <summary>How far down the bar, the row over it and the kinds reach in the column.</summary>
  public float Height => IsGradient ? GradientHeight : SolidHeight;

  /// <summary>The picked stop's place among the stops in order, counting from 1.</summary>
  public int PickedRank => OverlayGradientEdits.Order(_working.Gradient).IndexOf(_stop) + 1;

  /// <summary>
  /// Takes the color an editor shows; <paramref name="keepStop"/> stays on the
  /// picked stop when it still exists, as after undo.
  /// </summary>
  public void Load(OverlayColorSettings working, bool keepStop)
  {
    _working = working;
    _dragging = -1;
    if (_working.IsGradient && !(keepStop && _stop >= 0 && _stop < _working.Gradient.Stops.Count))
      _stop = OverlayGradientEdits.Order(_working.Gradient)[0];
  }

  /// <summary>Takes the color again after the editor changed it, keeping the picked stop.</summary>
  public void Reload(OverlayColorSettings working)
  {
    _working = working;
    if (IsGradient)
      _stop = Mathf.Clamp(_stop, 0, _working.Gradient.Stops.Count - 1);
  }

  /// <summary>No color is being edited: the picker shows a plain color.</summary>
  public void Clear() => _working = null;

  /// <summary>The picked stop's color, or the solid color, as the controls show it.</summary>
  public Color WorkingColor()
  {
    if (IsGradient)
    {
      OverlayGradientStop stop = _working.Gradient.Stops[_stop];
      Color picked = _host.FollowsHighlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
      picked.a = StopAlpha(_stop);
      return picked;
    }
    Color color = _host.FollowsHighlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(_working.Hex);
    color.a = Mathf.Clamp01(_working.Alpha);
    return color;
  }

  private Button IconButton(string name, Transform parent, UiIconKind kind, Action click, float size)
  {
    // Opaque, so what is behind the picker never shows through a button. The
    // palette tints a white capsule, so its colors are the ones drawn.
    Color rest = UiPalette.Wash(_under, .07f);
    UiCapsuleGraphic surface = _ui.Capsule(name, parent, 0, 0, size, size, Color.white);
    surface.raycastTarget = true;
    UiButton button = surface.gameObject.AddComponent<UiButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = surface;
    button.SetPalette(
      new ColorBlock
      {
        normalColor = rest,
        highlightedColor = UiPalette.Wash(_under, .15f),
        pressedColor = UiPalette.Wash(_under, .22f),
        selectedColor = rest,
        disabledColor = UiPalette.Wash(_under, .03f),
        colorMultiplier = 1,
        fadeDuration = _ui.ReduceMotion() ? 0 : .08f,
      }
    );
    button.onClick.AddListener(() => click());
    UiCursor.Attach(surface.gameObject, UiCursorKind.Pointer);
    float glyph = Mathf.Round(size * .53f);
    UiIcon icon = _ui.Rect("Icon", surface.transform, (size - glyph) * .5f, (size - glyph) * .5f, glyph, glyph)
      .gameObject.AddComponent<UiIcon>();
    icon.Kind = kind;
    icon.color = UiPalette.Foreground;
    icon.raycastTarget = false;
    return button;
  }

  // ---- Layout ----

  /// <summary>Lays the bar and, with a gradient, the kinds out <paramref name="column"/> wide in <see cref="Root"/>.</summary>
  public void Layout(float column)
  {
    _column = column;
    bool shown = _host.GradientShown;
    bool gradient = IsGradient;
    _bar.gameObject.SetActive(shown);
    _kindRow.gameObject.SetActive(gradient);
    _delete.gameObject.SetActive(gradient);
    if (!shown)
    {
      _tip.gameObject.SetActive(false);
      return;
    }
    float y = BarTop;
    UiFactory.Place(_bar, 0, y, column, BarHeight);
    y += BarHeight + GradientGap;
    if (gradient)
    {
      UiFactory.Place(_kindRow, 0, y, column, KindRow);
      const float icon = 34,
        gap = 6;
      float tiles = column - icon * 2 - gap - 10;
      float tile = Mathf.Floor((tiles - gap * 3) / 4);
      for (int i = 0; i < 4; i++)
        UiFactory.Place(_kindFrames[i].rectTransform, i * (tile + gap), 0, tile, KindRow);
      UiFactory.Place((RectTransform)_flip.transform, column - icon * 2 - gap, 0, icon, icon);
      UiFactory.Place((RectTransform)_rotate.transform, column - icon, 0, icon, icon);
    }
    Refresh();
  }

  /// <summary>Lays the bar out again at its width, after what shows changed: the picker opened, or a color was added.</summary>
  public void Relayout()
  {
    if (_column > 0)
      Layout(_column);
  }

  // The delete button stands over the picked square, kept on the bar.
  private void PlaceDelete()
  {
    if (!IsGradient)
      return;
    float x = Mathf.Clamp(StopX(_stop) - DeleteSize * .5f, 0, Mathf.Max(0, _bar.sizeDelta.x - DeleteSize));
    UiFactory.Place(
      (RectTransform)_delete.transform,
      _bar.anchoredPosition.x + x,
      LabelRow + 1,
      DeleteSize,
      DeleteSize
    );
  }

  // The x of a stop on the bar, from the bar's left.
  private float StopX(int index)
  {
    float column = _bar.sizeDelta.x;
    return BarInset + Position(index) * (column - BarInset * 2);
  }

  private float Position(int index)
  {
    if (!IsGradient || index < 0 || index >= _working.Gradient.Stops.Count)
      return 0;
    return Mathf.Clamp01(ShownPosition?.Invoke(index) ?? _working.Gradient.Stops[index].Position);
  }

  private float StopAlpha(int index)
  {
    if (!IsGradient)
      return _host.Value.a;
    return Mathf.Clamp01(ShownAlpha?.Invoke(index) ?? _working.Gradient.Stops[index].Alpha);
  }

  /// <summary>Redraws the bar, its squares and the kind tiles from the working color.</summary>
  public void Refresh()
  {
    if (!_host.GradientShown || _working == null)
      return;
    int count = IsGradient ? _working.Gradient.Stops.Count : 1;
    while (_barStops.Count < count)
    {
      UiSwatchGraphic stop = _ui.Rect("Color stop", _bar, 0, 0, StopSize, StopSize)
        .gameObject.AddComponent<UiSwatchGraphic>();
      stop.raycastTarget = false;
      stop.Radius = 4.5f;
      _barStops.Add(stop);
    }
    for (int i = 0; i < _barStops.Count; i++)
      _barStops[i].gameObject.SetActive(i < count);
    if (!IsGradient)
    {
      Color solid = _host.Value;
      _barPreview.SetSolid(solid);
      PlaceStop(_barStops[0], BarInset, solid, false);
      return;
    }
    _previewGradient.Stops.Clear();
    for (int i = 0; i < count; i++)
      _previewGradient.Stops.Add(new OverlayGradientSample(Position(i), StopColor(i)));
    OverlayGradientMath.Sort(_previewGradient.Stops);
    _barPreview.SetGradient(_previewGradient);
    for (int i = 0; i < count; i++)
    {
      OverlayRgba rgba = StopColor(i);
      PlaceStop(_barStops[i], StopX(i), new Color(rgba.R, rgba.G, rgba.B, rgba.A), i == _stop);
    }
    // The picked square sits on top.
    _barStops[_stop].transform.SetAsLastSibling();
    PlaceDelete();
    for (int i = 0; i < 4; i++)
    {
      OverlayGradientMath.ResetHandles(_previewGradient.Geometry, (OverlayGradientKind)i);
      _kindPreviews[i].SetGradient(_previewGradient);
      bool current = (int)_working.Gradient.Kind == i;
      _kindFrames[i].color = current ? UiPalette.SoftAccent : new Color(.12f, .12f, .12f);
    }
  }

  private OverlayRgba StopColor(int index)
  {
    OverlayGradientStop stop = _working.Gradient.Stops[index];
    Color color = _host.FollowsHighlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    return new OverlayRgba(color.r, color.g, color.b, StopAlpha(index));
  }

  private void PlaceStop(UiSwatchGraphic stop, float x, Color color, bool picked)
  {
    float size = picked ? StopSize + 4 : StopSize;
    UiFactory.Place(stop.rectTransform, x - size * .5f, (BarHeight - size) * .5f, size, size);
    // The picked one is bigger, with a heavier edge; every edge follows its color.
    stop.Set(color, UiPalette.HandleEdge(color), picked ? 3.5f : 2.5f);
  }

  // ---- The bar ----

  private void BarDown(PointerEventData data)
  {
    if (!_host.GradientShown || !BarPoint(data, out float x))
      return;
    _tip.gameObject.SetActive(false);
    int hit = HitStop(x);
    if (hit >= 0)
    {
      SelectStop(hit);
      _dragging = hit;
      _grab = x - StopX(hit);
      return;
    }
    float position = Mathf.Clamp01((x - BarInset) / Mathf.Max(1, _bar.sizeDelta.x - BarInset * 2));
    if (_working.IsGradient && !FreeMove())
      position = OverlayGradientEdits.Snap(_working.Gradient, -1, position);
    int added = OverlayGradientEdits.AddStop(_working, position, AccentHex);
    if (added < 0)
      return;
    _stop = added;
    _dragging = added;
    _grab = 0;
    _host.Relayout();
    ShowStop();
    Emit();
    StopSelected?.Invoke();
  }

  private void BarDrag(PointerEventData data)
  {
    if (_dragging < 0 || !IsGradient || _dragging >= _working.Gradient.Stops.Count || !BarPoint(data, out float x))
      return;
    float position = Mathf.Clamp01((x - _grab - BarInset) / Mathf.Max(1, _bar.sizeDelta.x - BarInset * 2));
    if (!FreeMove())
      position = OverlayGradientEdits.Snap(_working.Gradient, _dragging, position);
    OverlayGradientStop stop = _working.Gradient.Stops[_dragging];
    if (Mathf.Approximately(stop.Position, position) && string.IsNullOrEmpty(stop.PositionFormula))
      return;
    stop.Position = position;
    stop.PositionFormula = string.Empty;
    OverlayGradientEdits.Sync(_working);
    Emit();
  }

  // While the color is solid, the bar says right above itself what a click
  // does, in the room the pill takes once there is a gradient: under the bar
  // it would cover the controls there.
  private void BarHover(PointerEventData data)
  {
    float x = 0;
    bool show = _host.GradientShown && !IsGradient && _dragging < 0 && BarPoint(data, out x) && HitStop(x) < 0;
    _tip.gameObject.SetActive(show);
    if (!show)
      return;
    float width = Mathf.Ceil(_tipText.GetPreferredValues(_tipText.text).x) + 22;
    float left = Mathf.Clamp(x - width * .5f, 0, Mathf.Max(0, _bar.sizeDelta.x - width));
    // In the layer the tip floats on (top left origin, y down).
    RectTransform layer = (RectTransform)_tip.parent;
    Rect area = _bar.rect;
    Vector3 corner = layer.InverseTransformPoint(_bar.TransformPoint(new Vector3(area.xMin + left, area.yMax, 0)));
    Rect inLayer = layer.rect;
    UiFactory.Place(_tip, corner.x - inLayer.xMin, inLayer.yMax - corner.y - TipHeight - TipGap, width, TipHeight);
    _tip.SetAsLastSibling();
  }

  private bool BarPoint(PointerEventData data, out float x)
  {
    x = 0;
    if (
      !RectTransformUtility.ScreenPointToLocalPointInRectangle(
        _bar,
        data.position,
        data.pressEventCamera ?? data.enterEventCamera,
        out Vector2 local
      )
    )
      return false;
    x = local.x - _bar.rect.xMin;
    return true;
  }

  private int HitStop(float x)
  {
    int count = IsGradient ? _working.Gradient.Stops.Count : 1;
    int hit = -1;
    float best = StopSize * .5f + 2;
    // The picked one first, as it is drawn on top.
    if (IsGradient && Mathf.Abs(x - StopX(_stop)) <= best)
      return _stop;
    for (int i = 0; i < count; i++)
    {
      float distance = Mathf.Abs(x - (IsGradient ? StopX(i) : BarInset));
      if (distance <= best)
      {
        best = distance;
        hit = i;
      }
    }
    return hit;
  }

  // Alt held: no snapping, as in the layout editor.
  private static bool FreeMove() => UiModifierKeys.Alt;

  private static string AccentHex => "#" + ColorUtility.ToHtmlStringRGB(UiTheme.Accent);

  // ---- Edits ----

  /// <summary>Picks another stop; the controls below show its color.</summary>
  public void SelectStop(int index)
  {
    if (!IsGradient || index < 0 || index >= _working.Gradient.Stops.Count || index == _stop)
      return;
    _stop = index;
    ShowStop();
    StopSelected?.Invoke();
  }

  /// <summary>Removes the picked color; one color left is solid again.</summary>
  public void RemoveSelectedStop()
  {
    if (!IsGradient)
      return;
    int next = OverlayGradientEdits.RemoveStop(_working, _stop);
    if (next < 0)
      return;
    _stop = next;
    _dragging = -1;
    _host.Relayout();
    _host.ShowColor(WorkingColor());
    Emit();
    StopSelected?.Invoke();
  }

  private void SetKind(OverlayGradientKind kind)
  {
    if (!IsGradient || _working.Gradient.Kind == kind)
      return;
    OverlayGradientEdits.SetKind(_working.Gradient, kind);
    Emit();
    Refresh();
  }

  private void Flip()
  {
    OverlayGradientEdits.Flip(_working);
    Emit();
    Refresh();
  }

  private void Rotate()
  {
    if (!IsGradient)
      return;
    Vector2 box = GradientBox?.Invoke() ?? Vector2.one;
    OverlayGradientEdits.Rotate(_working.Gradient, Mathf.Max(1, box.x), Mathf.Max(1, box.y));
    Emit();
    Refresh();
  }

  // The picked stop's color in the controls, without reporting a change.
  private void ShowStop()
  {
    if (IsGradient)
      _host.ShowColor(WorkingColor());
  }

  /// <summary>Tells the picker's owner the working color changed.</summary>
  public void Emit() => _host.Report(_working.Clone());

  /// <summary>
  /// The color as the editor gets it after the controls moved to
  /// <paramref name="color"/>: the picked stop changes, or the solid color.
  /// A stop's calculated opacity stays unless the opacity itself moved.
  /// </summary>
  public OverlayColorSettings TakeColor(Color color, float alphaBefore)
  {
    if (!IsGradient)
    {
      OverlayColorSettings solid = _host.FollowsHighlight
        ? OverlayColorSettings.HighlightColor(color.a)
        : OverlayColorSettings.Custom("#" + ColorUtility.ToHtmlStringRGB(color), color.a);
      _working = solid.Clone();
      return solid;
    }
    OverlayGradientStop stop = _working.Gradient.Stops[_stop];
    if (!_host.FollowsHighlight)
      stop.Hex = "#" + ColorUtility.ToHtmlStringRGB(color);
    if (Mathf.Abs(color.a - alphaBefore) > .0005f || string.IsNullOrEmpty(stop.AlphaFormula))
    {
      stop.Alpha = Mathf.Clamp01(color.a);
      stop.AlphaFormula = string.Empty;
    }
    OverlayGradientEdits.Sync(_working);
    return _working.Clone();
  }

  /// <summary>Makes every stop follow the highlight color, or each its own color again.</summary>
  public void SetHighlight(bool highlight) => OverlayGradientEdits.SetHighlight(_working, highlight, AccentHex);
}
