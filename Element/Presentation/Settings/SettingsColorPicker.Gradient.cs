using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Settings;

// The color bar on top of an overlay editor's picker. One color is solid;
// clicking the bar adds a color and makes a gradient. Its squares move along
// the bar, the picked one shows its position pill (owned by the editor) with
// a delete button beside it, and the square, hue and opacity controls below
// edit the picked color. Four tiles drawn in the current colors pick the
// kind, beside flip and a quarter turn. Following the highlight, only
// opacities differ.
internal sealed partial class SettingsColorPicker
{
  private const float BarHeight = 26,
    BarInset = 11,
    StopSize = 18,
    PillRow = 36,
    KindRow = 34,
    GradientGap = 10,
    NoteHeight = 34,
    DeleteSize = 28;

  private RectTransform _bar,
    _kindRow,
    _positionSlot,
    _tip;
  private SettingsGradientPreview _barPreview;
  private readonly List<SettingsSwatchGraphic> _barStops = new();
  private readonly Image[] _kindFrames = new Image[4];
  private readonly SettingsGradientPreview[] _kindPreviews = new SettingsGradientPreview[4];
  private Button _flip,
    _rotate,
    _delete;
  private TextMeshProUGUI _tipText,
    _highlightNote;
  private OverlayColorSettings _working;
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

  /// <summary>The color bar shows: in an overlay editor's picker.</summary>
  public bool GradientAvailable => _sourceMode && _column;

  public bool IsGradient => GradientAvailable && _working != null && _working.IsGradient;

  /// <summary>The picked stop's index in the gradient's stops; -1 for a solid color.</summary>
  public int SelectedStop => IsGradient ? _stop : -1;

  /// <summary>Where the editor puts the picked stop's position pill; see <see cref="PlacePosition"/>.</summary>
  public RectTransform PositionSlot => _positionSlot;

  // An editor's picker is two columns: the color bar with its pill above it,
  // the kinds and Highlight/Custom on the left; the color's own controls on
  // the right. The bar keeps its place whether the color is solid or not.
  private const float ColumnWidth = 256,
    ColumnGap = 20,
    TwoColumnWidth = ColumnWidth * 2 + ColumnGap,
    RightColumn = ColumnWidth + ColumnGap;

  // Where Highlight/Custom sits in the left column.
  private float SourceTop =>
    IsGradient ? PillRow + BarHeight + GradientGap + KindRow + GradientGap : PillRow + BarHeight + GradientGap + 4;

  // An editor's picker keeps one height, that of the custom color's column,
  // so switching Highlight/Custom or adding a color moves nothing; following
  // the highlight leaves room under its opacity.
  private const float TwoColumnHeight = 290;

  private float ColumnHeight =>
    GradientAvailable ? TwoColumnHeight
    : _followsHighlight ? 142
    : 334;

  private void LayoutTwoColumns(bool custom)
  {
    const float column = ColumnWidth,
      x = RightColumn;
    LayoutGradient(0, column);
    float source = SourceTop;
    SettingsUiFactory.Place(_source.Root, 0, source, column, 36);
    SettingsUiFactory.Place(_highlightNote.rectTransform, 0, source + 44, column, NoteHeight);
    if (custom)
    {
      const float square = 156;
      SettingsUiFactory.Place(_planeRect, x, 0, column, square);
      SettingsUiFactory.Place((RectTransform)_hue.transform, x - 10, square + 6, column + 20, 36);
      SettingsUiFactory.Place((RectTransform)_alpha.transform, x - 10, square + 38, column + 20, 36);
      SettingsUiFactory.Place(_error.rectTransform, x, square + 76, column, 14);
      float bottom = ColumnHeight - 40;
      SettingsUiFactory.Place((RectTransform)_hex.transform, x, bottom, 176, 40);
      SettingsUiFactory.Place((RectTransform)_opacity.transform, x + 184, bottom, 72, 40);
      SettingsUiFactory.Place(_opacitySlot, x + 184, bottom, 72, 40);
    }
    else
    {
      _opacityLabel.text = IsGradient
        ? StopOpacityName(OverlayGradientEdits.Order(_working.Gradient).IndexOf(_stop) + 1)
        : L("불투명도", "Opacity");
      SettingsUiFactory.Place(_opacityLabel.rectTransform, x, 0, 176, 40);
      SettingsUiFactory.Place((RectTransform)_opacity.transform, x + 184, 0, 72, 40);
      SettingsUiFactory.Place(_opacitySlot, x + 184, 0, 72, 40);
      SettingsUiFactory.Place((RectTransform)_alpha.transform, x - 10, 48, column + 20, 36);
      SettingsUiFactory.Place(_error.rectTransform, x, 84, column, 14);
    }
    Resize();
  }

  private void BuildGradient()
  {
    _bar = _ui.Rect("Color bar", Root, 0, 0, 256, BarHeight);
    SettingsCapsuleGraphic track = _ui.Capsule("Color bar track", _bar, 0, 0, 256, BarHeight, Color.white);
    track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    SettingsUiFactory.Stretch(track.rectTransform, 0, 0, 0, 0);
    _barPreview = _ui.Rect("Colors", track.transform, 0, 0, 256, BarHeight).gameObject.AddComponent<SettingsGradientPreview>();
    SettingsUiFactory.Stretch(_barPreview.rectTransform, 0, 0, 0, 0);
    _barPreview.raycastTarget = false;
    _barPreview.AlongX = true;
    _barPreview.Inset = BarInset;
    SettingsPointerArea area = _ui.Rect("Color bar input", _bar, 0, -4, 256, BarHeight + 8)
      .gameObject.AddComponent<SettingsPointerArea>();
    area.raycastTarget = true;
    area.Down = BarDown;
    area.Dragged = BarDrag;
    area.Up = _ => _dragging = -1;
    area.Moved = BarHover;
    area.Exited = () => _tip.gameObject.SetActive(false);
    SettingsCursor.Attach(area.gameObject, SettingsCursorKind.Pointer);

    _kindRow = _ui.Rect("Gradient kinds", Root, 0, 0, 256, KindRow);
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
      SettingsCursor.Attach(frame.gameObject, SettingsCursorKind.Pointer);
      Image mask = _ui.Panel("Mask", frame.transform, 3, 3, 32, KindRow - 6, Color.white);
      mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
      SettingsUiFactory.Stretch(mask.rectTransform, 3, 3, 3, 3);
      SettingsGradientPreview preview = _ui.Rect("Preview", mask.transform, 0, 0, 32, KindRow - 6)
        .gameObject.AddComponent<SettingsGradientPreview>();
      SettingsUiFactory.Stretch(preview.rectTransform, 0, 0, 0, 0);
      preview.raycastTarget = false;
      preview.Cell = 4;
      _kindFrames[i] = frame;
      _kindPreviews[i] = preview;
    }
    _flip = IconButton(L("뒤집기", "Flip"), _kindRow, SettingsGradientIconKind.Flip, Flip, KindRow);
    _rotate = IconButton(L("90° 돌리기", "Turn 90°"), _kindRow, SettingsGradientIconKind.Rotate, Rotate, KindRow);

    _positionSlot = _ui.Rect("Position slot", Root, 0, 0, 10, 32);
    _delete = IconButton(
      L("이 색 지우기", "Remove this color"),
      Root,
      SettingsGradientIconKind.Delete,
      RemoveSelectedStop,
      DeleteSize
    );

    SettingsCapsuleGraphic tip = _ui.Capsule("Add color tip", Root, 0, 0, 10, 26, new Color(.11f, .11f, .11f, .98f));
    _tip = tip.rectTransform;
    _tipText = _ui.Text(L("눌러서 색 추가", "Click to add a color"), _tip, 0, 0, 10, 26, 12);
    _tipText.alignment = TextAlignmentOptions.Midline;
    _tipText.textWrappingMode = TextWrappingModes.NoWrap;
    SettingsUiFactory.Stretch(_tipText.rectTransform, 0, 0, 0, 0);
    _tip.gameObject.SetActive(false);

    _highlightNote = _ui.Text(
      L("하이라이트 색 하나로 칠해요. 점마다 불투명도만 정해요.", "One highlight color: each stop sets only its opacity."),
      Root,
      0,
      0,
      256,
      NoteHeight,
      12,
      SettingsUiFactory.Muted
    );
    _highlightNote.alignment = TextAlignmentOptions.TopLeft;
  }

  private Button IconButton(string name, Transform parent, SettingsGradientIconKind kind, Action click, float size)
  {
    SettingsCapsuleGraphic surface = _ui.Capsule(name, parent, 0, 0, size, size, new Color(1, 1, 1, .07f));
    surface.raycastTarget = true;
    SettingsButton button = surface.gameObject.AddComponent<SettingsButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = surface;
    button.SetPalette(
      new ColorBlock
      {
        normalColor = new Color(1, 1, 1, .07f),
        highlightedColor = new Color(1, 1, 1, .15f),
        pressedColor = new Color(1, 1, 1, .22f),
        selectedColor = new Color(1, 1, 1, .07f),
        disabledColor = new Color(1, 1, 1, .03f),
        colorMultiplier = 1,
        fadeDuration = _ui.ReduceMotion() ? 0 : .08f,
      }
    );
    button.onClick.AddListener(() => click());
    SettingsCursor.Attach(surface.gameObject, SettingsCursorKind.Pointer);
    float glyph = Mathf.Round(size * .53f);
    SettingsGradientIcon icon = _ui.Rect("Icon", surface.transform, (size - glyph) * .5f, (size - glyph) * .5f, glyph, glyph)
      .gameObject.AddComponent<SettingsGradientIcon>();
    icon.Kind = kind;
    icon.color = SettingsUiFactory.Foreground;
    icon.raycastTarget = false;
    return button;
  }

  // ---- Layout ----

  // Lays the bar and, with a gradient, the kinds out in the column at `left`.
  private void LayoutGradient(float left, float column)
  {
    bool shown = GradientAvailable;
    bool gradient = IsGradient;
    _bar.gameObject.SetActive(shown);
    _kindRow.gameObject.SetActive(gradient);
    _positionSlot.gameObject.SetActive(gradient);
    _delete.gameObject.SetActive(gradient);
    _highlightNote.gameObject.SetActive(gradient && _followsHighlight);
    if (!shown)
    {
      _tip.gameObject.SetActive(false);
      return;
    }
    float y = PillRow;
    SettingsUiFactory.Place(_bar, left, y, column, BarHeight);
    y += BarHeight + GradientGap;
    if (gradient)
    {
      SettingsUiFactory.Place(_kindRow, left, y, column, KindRow);
      const float icon = 34,
        gap = 6;
      float tiles = column - icon * 2 - gap - 10;
      float tile = Mathf.Floor((tiles - gap * 3) / 4);
      for (int i = 0; i < 4; i++)
        SettingsUiFactory.Place(_kindFrames[i].rectTransform, i * (tile + gap), 0, tile, KindRow);
      SettingsUiFactory.Place((RectTransform)_flip.transform, column - icon * 2 - gap, 0, icon, icon);
      SettingsUiFactory.Place((RectTransform)_rotate.transform, column - icon, 0, icon, icon);
    }
    RefreshGradient();
  }

  /// <summary>
  /// Puts the editor's position pill, <paramref name="width"/> wide, over the
  /// picked square on the bar, with the delete button right beside it.
  /// </summary>
  public void PlacePosition(float width)
  {
    if (!IsGradient)
      return;
    Vector2 bar = _bar.anchoredPosition;
    float column = _bar.sizeDelta.x;
    float x = StopX(_stop) - width * .5f;
    x = Mathf.Clamp(x, 0, Mathf.Max(0, column - width - 30));
    SettingsUiFactory.Place(_positionSlot, bar.x + x, 0, width, 32);
    SettingsUiFactory.Place((RectTransform)_delete.transform, bar.x + x + width + 4, 2, DeleteSize, DeleteSize);
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
      return _value.a;
    return Mathf.Clamp01(ShownAlpha?.Invoke(index) ?? _working.Gradient.Stops[index].Alpha);
  }

  // Redraws the bar, its squares and the kind tiles from the working color.
  private void RefreshGradient()
  {
    if (!GradientAvailable || _working == null)
      return;
    int count = IsGradient ? _working.Gradient.Stops.Count : 1;
    while (_barStops.Count < count)
    {
      SettingsSwatchGraphic stop = _ui.Rect("Color stop", _bar, 0, 0, StopSize, StopSize)
        .gameObject.AddComponent<SettingsSwatchGraphic>();
      stop.raycastTarget = false;
      stop.Radius = 4.5f;
      _barStops.Add(stop);
    }
    for (int i = 0; i < _barStops.Count; i++)
      _barStops[i].gameObject.SetActive(i < count);
    if (!IsGradient)
    {
      Color solid = _value;
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
    for (int i = 0; i < 4; i++)
    {
      OverlayGradientMath.ResetHandles(_previewGradient.Geometry, (OverlayGradientKind)i);
      _kindPreviews[i].SetGradient(_previewGradient);
      bool current = (int)_working.Gradient.Kind == i;
      _kindFrames[i].color = current ? SettingsPalette.SoftAccent : new Color(.12f, .12f, .12f);
    }
  }

  private OverlayRgba StopColor(int index)
  {
    OverlayGradientStop stop = _working.Gradient.Stops[index];
    Color color = _followsHighlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    return new OverlayRgba(color.r, color.g, color.b, StopAlpha(index));
  }

  private void PlaceStop(SettingsSwatchGraphic stop, float x, Color color, bool picked)
  {
    float size = picked ? StopSize + 4 : StopSize;
    SettingsUiFactory.Place(stop.rectTransform, x - size * .5f, (BarHeight - size) * .5f, size, size);
    stop.Set(color, picked ? SettingsPalette.SoftAccent : Color.white);
  }

  // ---- The bar ----

  private void BarDown(PointerEventData data)
  {
    if (!GradientAvailable || !BarPoint(data, out float x))
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
    Layout();
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

  // While the color is solid, the bar says right under itself what a click does.
  private void BarHover(PointerEventData data)
  {
    float x = 0;
    bool show = GradientAvailable && !IsGradient && _dragging < 0 && BarPoint(data, out x) && HitStop(x) < 0;
    _tip.gameObject.SetActive(show);
    if (!show)
      return;
    float width = Mathf.Ceil(_tipText.GetPreferredValues(_tipText.text).x) + 22;
    Vector2 bar = _bar.anchoredPosition;
    float left = Mathf.Clamp(x - width * .5f, 0, Mathf.Max(0, _bar.sizeDelta.x - width));
    // Anchored y is the negative of the top.
    SettingsUiFactory.Place(_tip, bar.x + left, -bar.y + BarHeight + 6, width, 26);
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
  private static bool FreeMove() => SettingsModifierKeys.Alt;

  private static string AccentHex => "#" + ColorUtility.ToHtmlStringRGB(OverlayTheme.Accent);

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
    Layout();
    if (IsGradient)
      ShowStop();
    else
      ShowSolid();
    Emit();
    StopSelected?.Invoke();
  }

  private void SetKind(OverlayGradientKind kind)
  {
    if (!IsGradient || _working.Gradient.Kind == kind)
      return;
    OverlayGradientEdits.SetKind(_working.Gradient, kind);
    Emit();
    RefreshGradient();
  }

  private void Flip()
  {
    OverlayGradientEdits.Flip(_working);
    Emit();
    RefreshGradient();
  }

  private void Rotate()
  {
    if (!IsGradient)
      return;
    Vector2 box = GradientBox?.Invoke() ?? Vector2.one;
    OverlayGradientEdits.Rotate(_working.Gradient, Mathf.Max(1, box.x), Mathf.Max(1, box.y));
    Emit();
    RefreshGradient();
  }

  /// <summary>
  /// Takes the color again after the editor changed it (handles, the dock,
  /// undo), keeping the picked stop. The controls follow without reporting.
  /// </summary>
  public void Refresh(OverlayColorSettings value)
  {
    if (!Visible || !_sourceMode || value == null)
      return;
    bool wasGradient = IsGradient;
    _working = value.Clone();
    if (IsGradient)
      _stop = Mathf.Clamp(_stop, 0, _working.Gradient.Stops.Count - 1);
    bool layout = wasGradient != IsGradient;
    if (_followsHighlight != value.Highlight)
    {
      _followsHighlight = value.Highlight;
      _source.Selected = value.Highlight ? 0 : 1;
      layout = true;
    }
    if (layout)
      Layout();
    // Undo can change the color underneath; the controls show it again. A
    // color that already shows keeps its hue, which grays would lose.
    Color shown = IsGradient
      ? (_followsHighlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(_working.Gradient.Stops[_stop].Hex))
      : (_followsHighlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(_working.Hex));
    bool differs =
      Mathf.Abs(shown.r - _value.r) > .002f
      || Mathf.Abs(shown.g - _value.g) > .002f
      || Mathf.Abs(shown.b - _value.b) > .002f
      || (IsGradient && Mathf.Abs(StopAlpha(_stop) - _value.a) > .002f);
    if (differs)
    {
      if (IsGradient)
        ShowStop();
      else
        ShowSolid();
    }
    else
      RefreshGradient();
  }

  // The picked stop's color in the controls, without reporting a change.
  private void ShowStop()
  {
    if (!IsGradient)
      return;
    OverlayGradientStop stop = _working.Gradient.Stops[_stop];
    Color color = _followsHighlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    color.a = StopAlpha(_stop);
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, true, false);
  }

  private void ShowSolid()
  {
    Color color = _followsHighlight ? OverlayTheme.Accent : OverlayTextComposer.ParseHex(_working.Hex);
    color.a = Mathf.Clamp01(_working.Alpha);
    Color.RGBToHSV(color, out _h, out _s, out _v);
    Apply(color, true, false);
  }

  private void Emit() => _sourceChanged?.Invoke(_working.Clone());

  private static string StopOpacityName(int rank) => L(rank + "번째 색 불투명도", "Color " + rank + " opacity");

  /// <summary>
  /// The color as the editor gets it after the controls moved to
  /// <paramref name="color"/>: the picked stop changes, or the solid color.
  /// A stop's calculated opacity stays unless the opacity itself moved.
  /// </summary>
  private OverlayColorSettings SourceValue(Color color, float alphaBefore)
  {
    if (!IsGradient)
    {
      OverlayColorSettings solid = _followsHighlight
        ? OverlayColorSettings.HighlightColor(color.a)
        : OverlayColorSettings.Custom("#" + ColorUtility.ToHtmlStringRGB(color), color.a);
      _working = solid.Clone();
      return solid;
    }
    OverlayGradientStop stop = _working.Gradient.Stops[_stop];
    if (!_followsHighlight)
      stop.Hex = "#" + ColorUtility.ToHtmlStringRGB(color);
    if (Mathf.Abs(color.a - alphaBefore) > .0005f || string.IsNullOrEmpty(stop.AlphaFormula))
    {
      stop.Alpha = Mathf.Clamp01(color.a);
      stop.AlphaFormula = string.Empty;
    }
    OverlayGradientEdits.Sync(_working);
    return _working.Clone();
  }

  // Following the highlight or not changes the whole gradient at once.
  private bool SetGradientSource(bool highlight)
  {
    if (!IsGradient)
      return false;
    _followsHighlight = highlight;
    OverlayGradientEdits.SetHighlight(_working, highlight, AccentHex);
    Layout();
    ShowStop();
    Emit();
    return true;
  }
}
