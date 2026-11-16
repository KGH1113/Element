using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.ValueColors;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The band of a color that follows a value: its colors from the lowest stop
/// on the left to the highest on the right, each stop a round handle with its
/// value above it, and under the band a pointer at where the value is now.
/// A press on a handle picks it and drags it along (to tidy numbers, or freely
/// with Alt); a press on the band between handles adds a stop there in the
/// color the band already has. While a handle is dragged the band keeps its
/// scale, so the handle stays under the pointer; it fits the stops again once
/// let go.
/// </summary>
internal sealed class OverlayValueColorBandView : NativeView
{
  public const float LabelRow = 16,
    Track = 30,
    MarkerRow = 22,
    Height = LabelRow + Track + MarkerRow,
    Inset = 14;

  private const float Band = 16,
    Knob = 9,
    PickedKnob = 12,
    Lift = 2,
    Ring = 2,
    LabelGap = 5;

  private static readonly Color Ghost = new(1, 1, 1, .7f);

  private readonly OverlayValueColorModel _vm;
  private readonly OverlayValueColorRamp _ramp = new();
  private readonly List<OverlayValueColorKnob> _knobs = new();
  private readonly List<TextMeshProUGUI> _labels = new();
  private readonly float[] _labelLeft = new float[OverlayValueColor.MaximumStops];
  private readonly float[] _labelWidth = new float[OverlayValueColor.MaximumStops];
  private RectTransform _knobLayer,
    _labelLayer;
  private OverlayValueColorBandGraphic _band;
  private OverlayValueColorKnob _ghost;
  private OverlayValueMarkerGraphic _marker;
  private TextMeshProUGUI _markerText;
  private float _width;
  private float _low,
    _high = 100;
  private bool _frozen;
  private int _dragging = -1;
  private float _grab;

  public OverlayValueColorBandView(OverlayValueColorModel vm) => _vm = vm;

  /// <summary>Where a stop at <paramref name="value"/> sits, from the band's left end, on the scale shown now.</summary>
  public float X(float value)
  {
    float span = _high - _low;
    float t = span <= 1e-6f ? .5f : (value - _low) / span;
    return Inset + t * Mathf.Max(1, _width - Inset * 2);
  }

  /// <summary>The value at <paramref name="x"/> from the band's left end, on the scale shown now.</summary>
  public float Value(float x) => _low + (x - Inset) / Mathf.Max(1, _width - Inset * 2) * (_high - _low);

  protected override void Build(RectTransform rect)
  {
    rect.gameObject.name = "Value color band";
    UiPointerArea area = RectPeer.Child(rect, "Band input").gameObject.AddComponent<UiPointerArea>();
    RectPeer.Stretch(area.rectTransform, 0, 0, 0, 0);
    area.raycastTarget = true;
    area.Down = Down;
    area.Dragged = Drag;
    area.Up = _ => Release();
    area.Moved = Hover;
    area.Exited = () => _ghost.gameObject.SetActive(false);
    UiCursor.Attach(area.gameObject, UiCursorKind.Pointer);
    _band = RectPeer.Child(rect, "Band").gameObject.AddComponent<OverlayValueColorBandGraphic>();
    _band.raycastTarget = false;
    _ghost = RectPeer.Child(rect, "Add here").gameObject.AddComponent<OverlayValueColorKnob>();
    _ghost.raycastTarget = false;
    _ghost.gameObject.SetActive(false);
    _knobLayer = RectPeer.Child(rect, "Stops");
    _labelLayer = RectPeer.Child(rect, "Stop values");
    _marker = RectPeer.Child(rect, "Now").gameObject.AddComponent<OverlayValueMarkerGraphic>();
    _marker.raycastTarget = false;
    _marker.color = UiPalette.Foreground;
    _markerText = Label(rect, "Now value", 12);
    _markerText.richText = true;
    Watch(() => _vm.Revision.Value, _ => Redraw());
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 296, Height);

  protected override void Resized(float width, float height)
  {
    _width = width;
    Redraw();
  }

  // ---- Drawing ----

  private void Redraw()
  {
    if (_band == null || _width <= 0)
      return;
    OverlayValueColor color = _vm.Current;
    _ramp.Load(color);
    if (!_frozen)
      OverlayValueColorEdits.Range(color, out _low, out _high);
    Place(_band.rectTransform, 0, LabelRow + (Track - Band) * .5f, _width, Band);
    _band.Show(_ramp, UiTheme.Accent, _low, _high, Inset, _vm.Surface);
    int count = color.Stops.Count;
    while (_knobs.Count < count)
    {
      OverlayValueColorKnob knob = RectPeer.Child(_knobLayer, "Stop").gameObject.AddComponent<OverlayValueColorKnob>();
      knob.raycastTarget = false;
      _knobs.Add(knob);
      _labels.Add(Label(_labelLayer, "Stop value", 12));
    }
    for (int i = 0; i < _knobs.Count; i++)
    {
      bool shown = i < count;
      if (_knobs[i].gameObject.activeSelf != shown)
        _knobs[i].gameObject.SetActive(shown);
      if (_labels[i].gameObject.activeSelf != shown)
        _labels[i].gameObject.SetActive(shown);
    }
    for (int i = 0; i < count; i++)
      PlaceStop(i, color.Stops[i]);
    if (_vm.Selected >= 0 && _vm.Selected < count)
      _knobs[_vm.Selected].transform.SetAsLastSibling();
    PlaceLabels();
    PlaceMarker();
  }

  private void PlaceStop(int index, OverlayValueColorStop stop)
  {
    bool picked = index == _vm.Selected;
    float radius = picked ? PickedKnob : Knob;
    float size = (radius + 7) * 2;
    float x = Mathf.Clamp(X(stop.At), Inset, _width - Inset);
    float y = LabelRow + Track * .5f - (picked ? Lift : 0);
    Place(_knobs[index].rectTransform, x - size * .5f, y - size * .5f, size, size);
    Color fill = stop.Highlight ? UiTheme.Accent : OverlayTextComposer.ParseHex(stop.Hex);
    fill.a = Mathf.Clamp01(stop.Alpha);
    _knobs[index].Set(fill, UiPalette.Foreground, _vm.Surface, radius, picked ? Ring + .5f : Ring, picked);
    TextMeshProUGUI label = _labels[index];
    label.text = _vm.Format(stop.At);
    label.color = picked ? UiPalette.Foreground : UiPalette.Muted;
    _labelWidth[index] = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 2;
    _labelLeft[index] = x - _labelWidth[index] * .5f;
  }

  // Each value over its handle; values that would touch move apart, lowest
  // first, and stay inside the band's width.
  private void PlaceLabels()
  {
    int count = _ramp.Count;
    float right = 0;
    for (int rank = 0; rank < count; rank++)
    {
      int i = _ramp.Source(rank);
      float left = Mathf.Max(_labelLeft[i], rank == 0 ? 0 : right + LabelGap);
      _labelLeft[i] = Mathf.Max(0, left);
      right = _labelLeft[i] + _labelWidth[i];
    }
    float limit = _width;
    for (int rank = count - 1; rank >= 0; rank--)
    {
      int i = _ramp.Source(rank);
      if (_labelLeft[i] + _labelWidth[i] > limit)
        _labelLeft[i] = limit - _labelWidth[i];
      limit = _labelLeft[i] - LabelGap;
    }
    for (int rank = 0; rank < count; rank++)
    {
      int i = _ramp.Source(rank);
      Place(_labels[i].rectTransform, _labelLeft[i], 0, _labelWidth[i], LabelRow);
    }
  }

  // The pointer at the value now, held at the ends when the value is past them.
  private void PlaceMarker()
  {
    bool shown = !double.IsNaN(_vm.Live) && _ramp.Count > 0;
    if (_marker.gameObject.activeSelf != shown)
      _marker.gameObject.SetActive(shown);
    if (_markerText.gameObject.activeSelf != shown)
      _markerText.gameObject.SetActive(shown);
    if (!shown)
      return;
    float x = Mathf.Clamp(X((float)_vm.Live), Inset, _width - Inset);
    float top = LabelRow + Track - 3;
    Place(_marker.rectTransform, x - 5, top, 10, 6);
    string now = _vm.LiveIsSample ? UiLocale.L("예시", "Sample") : UiLocale.L("지금", "Now");
    _markerText.text = "<color=#A8A8A8>" + now + "</color>  " + _vm.Format(_vm.Live);
    float width = Mathf.Ceil(_markerText.GetPreferredValues(_markerText.text).x) + 2;
    float left = Mathf.Clamp(x - width * .5f, 0, Mathf.Max(0, _width - width));
    Place(_markerText.rectTransform, left, top + 5, width, MarkerRow - 5);
  }

  // ---- Gestures ----

  private void Down(PointerEventData data)
  {
    if (!Point(data, out float x, out float y))
      return;
    _ghost.gameObject.SetActive(false);
    int hit = Hit(x, y);
    if (hit >= 0)
    {
      _vm.Editor.SelectStop(hit);
      Begin(hit, x - Mathf.Clamp(X(_vm.Current.Stops[hit].At), Inset, _width - Inset));
      return;
    }
    if (!OnBand(y))
      return;
    float at = Value(x);
    if (!UiModifierKeys.Alt)
      at = OverlayValueColorEdits.Snap(at, _low, _high);
    int added = _vm.Editor.AddStop(at);
    if (added >= 0)
      Begin(added, 0);
  }

  private void Begin(int index, float grab)
  {
    _frozen = true;
    _dragging = index;
    _grab = grab;
    _vm.Editor.BeginDrag(index);
  }

  private void Drag(PointerEventData data)
  {
    if (_dragging < 0 || !Point(data, out float x, out _))
      return;
    float at = Value(x - _grab);
    if (!UiModifierKeys.Alt)
      at = OverlayValueColorEdits.Snap(at, _low, _high);
    // Past the ends the value goes on, so the band can grow; within reason.
    float span = Mathf.Max(1e-3f, _high - _low);
    at = Mathf.Clamp(at, _low - span * 2, _high + span * 2);
    _vm.Editor.DragStop(_dragging, at);
  }

  private void Release()
  {
    if (_dragging < 0)
      return;
    _dragging = -1;
    _frozen = false;
    _vm.Editor.EndDrag();
    Redraw();
  }

  // Over the band between handles: a ring shows where a click adds a stop.
  private void Hover(PointerEventData data)
  {
    float x = 0;
    bool shown = _dragging < 0 && Point(data, out x, out float y) && OnBand(y) && Hit(x, y) < 0;
    if (_ghost.gameObject.activeSelf != shown)
      _ghost.gameObject.SetActive(shown);
    if (!shown)
      return;
    float size = (Knob + 3) * 2;
    Place(
      _ghost.rectTransform,
      Mathf.Clamp(x, Inset, _width - Inset) - size * .5f,
      LabelRow + Track * .5f - size * .5f,
      size,
      size
    );
    _ghost.SetHollow(Ghost, Knob - 1);
  }

  private static bool OnBand(float y) => y >= LabelRow && y <= LabelRow + Track;

  // The handle under a point, the picked one first as it is drawn on top; a
  // value over a handle picks it too.
  private int Hit(float x, float y)
  {
    if (y > LabelRow + Track)
      return -1;
    List<OverlayValueColorStop> stops = _vm.Current.Stops;
    if (y < LabelRow)
    {
      for (int i = 0; i < stops.Count; i++)
        if (x >= _labelLeft[i] - 2 && x <= _labelLeft[i] + _labelWidth[i] + 2)
          return i;
      return -1;
    }
    int selected = _vm.Selected;
    if (selected >= 0 && selected < stops.Count && Mathf.Abs(x - KnobX(stops[selected])) <= PickedKnob + 3)
      return selected;
    int hit = -1;
    float best = Knob + 4;
    for (int i = 0; i < stops.Count; i++)
    {
      float distance = Mathf.Abs(x - KnobX(stops[i]));
      if (distance <= best)
      {
        best = distance;
        hit = i;
      }
    }
    return hit;
  }

  private float KnobX(OverlayValueColorStop stop) => Mathf.Clamp(X(stop.At), Inset, _width - Inset);

  // A screen point in the band's units from its top left.
  private bool Point(PointerEventData data, out float x, out float y)
  {
    x = y = 0;
    RectTransform rect = Rect;
    if (
      rect == null
      || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
        rect,
        data.position,
        data.pressEventCamera ?? data.enterEventCamera,
        out Vector2 local
      )
    )
      return false;
    Rect bounds = rect.rect;
    x = local.x - bounds.xMin;
    y = bounds.yMax - local.y;
    return true;
  }

  private TextMeshProUGUI Label(RectTransform parent, string name, float size)
  {
    TextMeshProUGUI label = RectPeer.Child(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
    if (UguiTheme.Font != null)
      label.font = UguiTheme.Font;
    label.fontSize = size;
    label.alignment = TextAlignmentOptions.Midline;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.raycastTarget = false;
    label.color = UiPalette.Muted;
    return label;
  }

  private static void Place(RectTransform rect, float x, float y, float width, float height)
  {
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
    rect.anchoredPosition = new Vector2(x, -y);
    rect.sizeDelta = new Vector2(width, height);
  }
}
