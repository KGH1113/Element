using System;
using System.Collections.Generic;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Layout;

/// <summary>
/// A gradient's handles over an editor's preview. Circles are handles and
/// squares are colors: the round start and end handles sit on the line and
/// only move the gradient, and the colors hang just off the line as squares
/// with a notch pointing at where they sit, so the two never cover each
/// other. Radial, angular and diamond gradients add a width handle and an
/// outline of their reach. Handles stick to the box's edges, middle and
/// center and to each other's lines (Alt lets go, Shift turns in 15° steps),
/// with guides like the layout editor's. The angle shows in a pill beside the
/// line, on the side without squares.
/// </summary>
internal sealed class OverlayGradientHandles
{
  /// <summary>How near (frame units) a handle comes before it sticks.</summary>
  public const float SnapDistance = 8;

  private const float GripSize = 24,
    SquareSize = 24,
    SquareHalf = SquareSize * .5f,
    LineHit = 18;

  private readonly SettingsUiFactory _ui;
  private readonly Func<Vector2, Vector2> _toFrame;
  private readonly RectTransform _root;
  private readonly OverlayGradientLines _lines;
  private readonly RectTransform _lineArea;
  private readonly Grip[] _grips = new Grip[3];
  private readonly List<Square> _squares = new();
  private readonly List<OverlayGradientSample> _order = new();

  private OverlayColorSettings _color;
  private int _selected;
  private Func<int, float> _shownPosition;
  private Func<int, Color> _stopColor;
  private Vector2 _origin;
  private float _scale = 1,
    _width = 1,
    _height = 1;

  // The drag in progress: which handle, where it started, the handles then.
  private int _dragging = -1;
  private int _dragStop = -1;
  private float _dragGrab;
  private Vector2 _dragStart;
  private NVector2 _start0,
    _end0,
    _across0;
  private bool _free;
  private float _guideX = float.NaN,
    _guideY = float.NaN;
  private bool _even;

  /// <summary>The handles are about to move: the editor remembers the color for undo.</summary>
  public Action Began;

  /// <summary>The handles or a color's position moved; the editor redraws.</summary>
  public Action Changed;

  /// <summary>A square was pressed; its stop is now the picked one.</summary>
  public Action<int> Picked;

  /// <summary>The line was clicked at a position (0–1): the editor adds a color there.</summary>
  public Action<float> Added;

  public OverlayGradientHandles(
    SettingsUiFactory ui,
    Transform parent,
    Func<Vector2, Vector2> toFrame,
    Action activateAngle,
    bool reduceMotion
  )
  {
    _ui = ui;
    _toFrame = toFrame;
    _root = ui.Rect("Gradient handles", parent, 0, 0, 10, 10);
    SettingsUiFactory.Stretch(_root, 0, 0, 0, 0);
    _lines = ui.Rect("Gradient lines", _root, 0, 0, 10, 10).gameObject.AddComponent<OverlayGradientLines>();
    SettingsUiFactory.Stretch(_lines.rectTransform, 0, 0, 0, 0);
    _lines.raycastTarget = false;
    _lineArea = ui.Rect("Gradient line", _root, 0, 0, 10, LineHit);
    _lineArea.pivot = new Vector2(0, .5f);
    SettingsPointerArea line = _lineArea.gameObject.AddComponent<SettingsPointerArea>();
    line.raycastTarget = true;
    line.Down = LineDown;
    line.Dragged = SquareDrag;
    line.Up = _ => EndDrag();
    SettingsCursor.Attach(line.gameObject, SettingsCursorKind.Pointer);
    for (int i = 0; i < 3; i++)
      _grips[i] = new Grip(ui, _root, i, this);
    Angle = new OverlayFormulaField(ui, _root, "Gradient angle", "°", activateAngle, reduceMotion);
    _root.gameObject.SetActive(false);
  }

  /// <summary>The angle pill; the editor shows its number and types it in the dock.</summary>
  public OverlayFormulaField Angle { get; }

  public bool Visible => _root.gameObject.activeSelf;

  public bool Dragging => _dragging >= 0 || _dragStop >= 0;

  /// <summary>
  /// Shows <paramref name="color"/>'s gradient over a box whose top left is
  /// <paramref name="origin"/> (frame units) and <paramref name="width"/> by
  /// <paramref name="height"/> units, <paramref name="scale"/> frame units
  /// each. The angle pill is placed after the editor set its number.
  /// </summary>
  public void Show(
    OverlayColorSettings color,
    int selected,
    Vector2 origin,
    float scale,
    float width,
    float height,
    Func<int, float> shownPosition,
    Func<int, Color> stopColor
  )
  {
    if (color == null || !color.IsGradient)
    {
      Hide();
      return;
    }
    _color = color;
    _selected = selected;
    _origin = origin;
    _scale = Mathf.Max(.001f, scale);
    _width = Mathf.Max(.001f, width);
    _height = Mathf.Max(.001f, height);
    _shownPosition = shownPosition;
    _stopColor = stopColor;
    if (!_root.gameObject.activeSelf)
      _root.gameObject.SetActive(true);
    Draw();
  }

  public void Hide()
  {
    if (_root.gameObject.activeSelf)
      _root.gameObject.SetActive(false);
    _dragging = _dragStop = -1;
    _color = null;
  }

  /// <summary>Whether a screen point is over a handle, a square, the line or the angle pill.</summary>
  public bool Contains(Vector2 screen)
  {
    if (!Visible)
      return false;
    if (Angle.Contains(screen) || OverlayEditorUi.Contains(_lineArea, screen))
      return true;
    foreach (Grip grip in _grips)
      if (OverlayEditorUi.Contains(grip.Rect, screen))
        return true;
    foreach (Square square in _squares)
      if (OverlayEditorUi.Contains(square.Rect, screen))
        return true;
    return false;
  }

  /// <summary>The stop whose square is under a screen point, or -1.</summary>
  public int SquareAt(Vector2 screen)
  {
    if (!Visible)
      return -1;
    for (int i = _squares.Count - 1; i >= 0; i--)
      if (_squares[i].Rect.gameObject.activeSelf && OverlayEditorUi.Contains(_squares[i].Rect, screen))
        return _squares[i].Stop;
    return -1;
  }

  // ---- Geometry ----

  private OverlayGradient Gradient => _color.Gradient;

  private Vector2 ToFrame(NVector2 units) => _origin + new Vector2(units.X, units.Y) * _scale;

  private NVector2 ToUnits(Vector2 frame)
  {
    Vector2 local = (frame - _origin) / _scale;
    return new NVector2(local.x, local.y);
  }

  private void Handles(out NVector2 start, out NVector2 end, out NVector2 across) =>
    OverlayGradientMath.Handles(Gradient, _width, _height, out start, out end, out across);

  // Where a stop sits on the line, or on the ring for angular (units).
  private NVector2 StopPoint(NVector2 start, NVector2 end, NVector2 across, float position)
  {
    if (Gradient.Kind == OverlayGradientKind.Angular)
    {
      double angle = position * Math.PI * 2;
      return start + (end - start) * (float)Math.Cos(angle) + (across - start) * (float)Math.Sin(angle);
    }
    return NVector2.Lerp(start, end, position);
  }

  // The side colors hang on: below the line, or right of an upright one (frame, y down).
  private static Vector2 Side(Vector2 start, Vector2 end)
  {
    Vector2 along = end - start;
    Vector2 side = along.sqrMagnitude > 1e-6f ? new Vector2(-along.y, along.x).normalized : Vector2.up;
    if (side.y < -1e-3f || (Mathf.Abs(side.y) <= 1e-3f && side.x < 0))
      side = -side;
    return side;
  }

  private float Shown(int index) =>
    Mathf.Clamp01(_shownPosition?.Invoke(index) ?? Gradient.Stops[index].Position);

  // ---- Drawing ----

  private void Draw()
  {
    Handles(out NVector2 start, out NVector2 end, out NVector2 across);
    Vector2 a = ToFrame(start),
      b = ToFrame(end),
      c = ToFrame(across);
    bool linear = Gradient.Kind == OverlayGradientKind.Linear,
      angular = Gradient.Kind == OverlayGradientKind.Angular;
    _lines.Begin(_root.rect);
    // Snap targets and guides only while a handle moves.
    if (_dragging >= 0 && !_free)
      DrawSnaps(_dragging == 0 ? a : _dragging == 1 ? b : c);
    if (!linear)
    {
      Color outline = _even ? SettingsPalette.SoftAccent : new Color(1, 1, 1, angular ? .95f : .75f);
      _lines.Outline(Reach(start, end, across), outline, angular ? 1.6f : 1.3f, !angular);
    }
    _lines.Segment(a, b, new Color(1, 1, 1, .95f), 1.6f);
    _lines.End();

    // The line takes clicks to add a color (the ring of angular doesn't).
    _lineArea.gameObject.SetActive(!angular);
    if (!angular)
    {
      Vector2 along = b - a;
      _lineArea.anchorMin = _lineArea.anchorMax = new Vector2(0, 1);
      _lineArea.pivot = new Vector2(0, .5f);
      _lineArea.anchoredPosition = new Vector2(a.x, -a.y);
      _lineArea.sizeDelta = new Vector2(Mathf.Max(1, along.magnitude), LineHit);
      _lineArea.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
    }

    // Colors hang off the line; the picked one is drawn last, on top.
    int count = Gradient.Stops.Count;
    while (_squares.Count < count)
      _squares.Add(new Square(_ui, _root, this));
    Vector2 side = Side(a, b);
    for (int i = 0; i < _squares.Count; i++)
    {
      Square square = _squares[i];
      bool shown = i < count;
      if (square.Rect.gameObject.activeSelf != shown)
        square.Rect.gameObject.SetActive(shown);
      if (!shown)
        continue;
      square.Stop = i;
      Vector2 at = ToFrame(StopPoint(start, end, across, Shown(i)));
      Vector2 n = side;
      if (angular)
      {
        Vector2 out_ = at - a;
        n = out_.sqrMagnitude > 1e-6f ? out_.normalized : Vector2.right;
      }
      float k = SquareHalf / Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.y));
      Vector2 center = at + n * (k + 9);
      SettingsUiFactory.Place(square.Rect, center.x - SquareHalf, center.y - SquareHalf, SquareSize, SquareSize);
      Color color = _stopColor?.Invoke(i) ?? Color.white;
      square.Graphic.Set(color, i == _selected ? SettingsPalette.SoftAccent : Color.white);
      // Local space has y up.
      square.Graphic.SetNotch(new Vector2(-n.x, n.y), k + 6);
    }
    if (_selected >= 0 && _selected < count)
      _squares[_selected].Rect.SetAsLastSibling();

    // Handles on top of everything else here.
    for (int i = 0; i < 3; i++)
    {
      bool shown = i < 2 || !linear;
      Grip grip = _grips[i];
      if (grip.Rect.gameObject.activeSelf != shown)
        grip.Rect.gameObject.SetActive(shown);
      if (!shown)
        continue;
      Vector2 at = i == 0 ? a : i == 1 ? b : c;
      SettingsUiFactory.Place(grip.Rect, at.x - GripSize * .5f, at.y - GripSize * .5f, GripSize, GripSize);
      bool stuck = _dragging == i && !_free && (!float.IsNaN(_guideX) || !float.IsNaN(_guideY) || (_even && i == 2));
      grip.Paint(stuck, i == 2);
      grip.Rect.SetAsLastSibling();
    }
    Angle.Root.SetAsLastSibling();
  }

  /// <summary>Puts the angle pill beside the middle of the line, away from the squares.</summary>
  public void PlaceAngle()
  {
    if (!Visible)
      return;
    Handles(out NVector2 start, out NVector2 end, out _);
    Vector2 a = ToFrame(start),
      b = ToFrame(end);
    Vector2 middle = (a + b) * .5f - Side(a, b) * 28;
    Rect bounds = _root.rect;
    float x = Mathf.Clamp(middle.x - Angle.Width * .5f, 8, Mathf.Max(8, bounds.width - Angle.Width - 8));
    float y = Mathf.Clamp(middle.y - OverlayFormulaField.Height * .5f, 8, Mathf.Max(8, bounds.height - 40));
    Angle.Place(x, y);
  }

  // The outline of how far a radial, angular or diamond gradient reaches (frame).
  private List<Vector2> Reach(NVector2 start, NVector2 end, NVector2 across)
  {
    List<Vector2> points = new(73);
    if (Gradient.Kind == OverlayGradientKind.Diamond)
    {
      NVector2 u = end - start,
        v = across - start;
      points.Add(ToFrame(start + u));
      points.Add(ToFrame(start + v));
      points.Add(ToFrame(start - u));
      points.Add(ToFrame(start - v));
      return points;
    }
    for (int i = 0; i < 72; i++)
      points.Add(ToFrame(StopPoint(start, end, across, i / 72f)));
    return points;
  }

  // Nine faint dots where a handle can stick, and a guide on each axis it stuck to.
  private void DrawSnaps(Vector2 handle)
  {
    Vector2 topLeft = _origin,
      size = new Vector2(_width, _height) * _scale;
    const float overhang = 9;
    Color guide = new(1, 1, 1, .7f);
    if (!float.IsNaN(_guideX))
    {
      float x = _origin.x + _guideX * _scale;
      _lines.Segment(
        new Vector2(x, Mathf.Min(topLeft.y - overhang, handle.y)),
        new Vector2(x, Mathf.Max(topLeft.y + size.y + overhang, handle.y)),
        guide,
        1
      );
    }
    if (!float.IsNaN(_guideY))
    {
      float y = _origin.y + _guideY * _scale;
      _lines.Segment(
        new Vector2(Mathf.Min(topLeft.x - overhang, handle.x), y),
        new Vector2(Mathf.Max(topLeft.x + size.x + overhang, handle.x), y),
        guide,
        1
      );
    }
    for (int row = 0; row < 3; row++)
    for (int column = 0; column < 3; column++)
    {
      float ux = _width * column * .5f,
        uy = _height * row * .5f;
      bool hit = _guideX == ux && _guideY == uy;
      Vector2 at = _origin + new Vector2(ux, uy) * _scale;
      _lines.Dot(at, hit ? 4.5f : 2.5f, hit ? SettingsPalette.SoftAccent : new Color(1, 1, 1, .55f));
    }
  }

  // ---- Dragging handles ----

  private void GripDown(int index, PointerEventData data)
  {
    if (_color == null)
      return;
    Began?.Invoke();
    _dragging = index;
    _dragStart = _toFrame(data.position);
    Handles(out _start0, out _end0, out _across0);
    _free = false;
    _guideX = _guideY = float.NaN;
    _even = false;
    Draw();
  }

  private void GripDrag(int index, PointerEventData data)
  {
    if (_dragging != index || _color == null)
      return;
    NVector2 pointer = ToUnits(_toFrame(data.position));
    bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    bool stick = !alt && !shift;
    _free = !stick;
    _guideX = _guideY = float.NaN;
    _even = false;
    NVector2 start = _start0,
      end = _end0,
      across = _across0;
    bool linear = Gradient.Kind == OverlayGradientKind.Linear;
    float tolerance = SnapDistance / _scale;

    // Shift turns about the other end in 15° steps; otherwise the point sticks.
    NVector2 Aim(NVector2 from, NVector2 to)
    {
      if (shift)
        return from + Turn(to - from);
      if (!stick)
        return to;
      return Stick(to, from, tolerance);
    }

    if (index == 0 && linear)
      start = Aim(end, pointer);
    else if (index == 1 && linear)
      end = Aim(start, pointer);
    else if (index == 0)
    {
      // The center moves the whole gradient.
      NVector2 moved = _start0 + ToUnits(_toFrame(data.position)) - ToUnits(_dragStart);
      _free = alt;
      if (!alt)
        moved = Stick(moved, null, tolerance);
      NVector2 shift0 = moved - _start0;
      start = _start0 + shift0;
      end = _end0 + shift0;
      across = _across0 + shift0;
    }
    else if (index == 1)
    {
      NVector2 u0 = _end0 - _start0,
        v0 = _across0 - _start0;
      float reach = v0.Length();
      float side = Math.Sign(u0.X * v0.Y - u0.Y * v0.X);
      if (side == 0)
        side = 1;
      NVector2 target = Aim(_start0, pointer);
      NVector2 u = target - _start0;
      float length = u.Length();
      if (length < 4 / _scale)
        return;
      end = target;
      across = _start0 + new NVector2(-u.Y, u.X) / length * reach * side;
    }
    else
    {
      // The width handle slides square to the end handle.
      NVector2 u = _end0 - _start0,
        v = _across0 - _start0;
      float uLength = Math.Max(1e-4f, u.Length());
      float side = Math.Sign(u.X * v.Y - u.Y * v.X);
      if (side == 0)
        side = 1;
      NVector2 n = new NVector2(-u.Y, u.X) / uLength * side;
      float length = Math.Max(6 / _scale, Math.Abs(NVector2.Dot(pointer - _start0, n)));
      if (shift)
        length = uLength;
      else if (stick)
        length = StickReach(n, length, uLength, tolerance);
      across = _start0 + n * length;
    }
    OverlayGradientMath.SetHandles(Gradient, _width, _height, start, end, across);
    Gradient.AngleFormula = string.Empty;
    Changed?.Invoke();
  }

  // A point sticks to the box's edges and middle on each axis, or to the
  // lines of `other` when given, as the layout editor aligns boxes.
  private NVector2 Stick(NVector2 point, NVector2? other, float tolerance)
  {
    float bestX = tolerance,
      bestY = tolerance;
    float x = point.X,
      y = point.Y;
    void TryX(float line)
    {
      float d = Math.Abs(line - point.X);
      if (d < bestX)
      {
        bestX = d;
        x = line;
        _guideX = line;
      }
    }
    void TryY(float line)
    {
      float d = Math.Abs(line - point.Y);
      if (d < bestY)
      {
        bestY = d;
        y = line;
        _guideY = line;
      }
    }
    for (int i = 0; i <= 2; i++)
    {
      TryX(_width * i * .5f);
      TryY(_height * i * .5f);
    }
    if (other.HasValue)
    {
      TryX(other.Value.X);
      TryY(other.Value.Y);
    }
    return new NVector2(x, y);
  }

  // The width handle sticks where its line meets the box's edges or middle,
  // or where the shape turns even (a circle, or a square diamond).
  private float StickReach(NVector2 n, float length, float even, float tolerance)
  {
    float best = length,
      distance = tolerance;
    for (int i = 0; i <= 2; i++)
    {
      if (Math.Abs(n.X) > .15f)
      {
        float line = _width * i * .5f;
        float reach = (line - _start0.X) / n.X;
        float d = Math.Abs(reach - length) * Math.Abs(n.X);
        if (reach > 0 && d < distance)
        {
          distance = d;
          best = reach;
          _guideX = line;
          _guideY = float.NaN;
          _even = false;
        }
      }
      if (Math.Abs(n.Y) > .15f)
      {
        float line = _height * i * .5f;
        float reach = (line - _start0.Y) / n.Y;
        float d = Math.Abs(reach - length) * Math.Abs(n.Y);
        if (reach > 0 && d < distance)
        {
          distance = d;
          best = reach;
          _guideY = line;
          _guideX = float.NaN;
          _even = false;
        }
      }
    }
    if (Math.Abs(even - length) < distance)
    {
      best = even;
      _guideX = _guideY = float.NaN;
      _even = true;
    }
    return best;
  }

  private static NVector2 Turn(NVector2 direction)
  {
    float length = direction.Length();
    double step = Math.PI / 12;
    double angle = Math.Round(Math.Atan2(direction.Y, direction.X) / step) * step;
    return new NVector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;
  }

  // ---- Colors on the line ----

  private void SquareDown(int stop, PointerEventData data)
  {
    if (_color == null || stop < 0 || stop >= Gradient.Stops.Count)
      return;
    Picked?.Invoke(stop);
    Began?.Invoke();
    _dragStop = stop;
    // Keep the point under the pointer where it was grabbed on the square.
    _dragGrab =
      Gradient.Kind == OverlayGradientKind.Angular ? 0 : PositionAt(_toFrame(data.position)) - Shown(stop);
  }

  private void SquareDrag(PointerEventData data)
  {
    if (_dragStop < 0 || _color == null || _dragStop >= Gradient.Stops.Count)
      return;
    float position = Mathf.Clamp01(PositionAt(_toFrame(data.position)) - _dragGrab);
    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    if (!alt)
      position = OverlayGradientEdits.Snap(Gradient, _dragStop, position);
    OverlayGradientStop stop = Gradient.Stops[_dragStop];
    stop.Position = position;
    stop.PositionFormula = string.Empty;
    OverlayGradientEdits.Sync(_color);
    Changed?.Invoke();
  }

  private void LineDown(PointerEventData data)
  {
    if (_color == null || Gradient.Stops.Count >= OverlayGradient.MaximumStops)
      return;
    float position = PositionAt(_toFrame(data.position));
    bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    if (!alt)
      position = OverlayGradientEdits.Snap(Gradient, -1, position);
    int before = Gradient.Stops.Count;
    Added?.Invoke(position);
    // The new color follows the pointer until it lets go.
    if (_color != null && Gradient.Stops.Count > before)
    {
      _dragStop = Gradient.Stops.Count - 1;
      _dragGrab = 0;
    }
  }

  private void EndDrag()
  {
    bool moved = _dragging >= 0;
    _dragging = _dragStop = -1;
    _guideX = _guideY = float.NaN;
    _even = false;
    if (moved && _color != null)
      Changed?.Invoke();
  }

  // Where a frame point falls along the line (0–1), or round the ring for angular.
  private float PositionAt(Vector2 frame)
  {
    Handles(out NVector2 start, out NVector2 end, out NVector2 across);
    NVector2 point = ToUnits(frame);
    if (Gradient.Kind == OverlayGradientKind.Angular)
      return new OverlayGradientFrame(OverlayGradientKind.Angular, start, end, across).At(point.X, point.Y);
    NVector2 u = end - start;
    float lengthSquared = u.LengthSquared();
    if (lengthSquared < 1e-6f)
      return 0;
    return Mathf.Clamp01(NVector2.Dot(point - start, u) / lengthSquared);
  }

  // ---- Pieces ----

  private sealed class Grip
  {
    private readonly SettingsCapsuleGraphic _ring,
      _dot;

    public Grip(SettingsUiFactory ui, Transform parent, int index, OverlayGradientHandles owner)
    {
      Rect = ui.Rect(index == 0 ? "Gradient start" : index == 1 ? "Gradient end" : "Gradient width", parent, 0, 0, GripSize, GripSize);
      _ring = ui.Capsule("Edge", Rect, 4.5f, 4.5f, 15, 15, new Color(0, 0, 0, .55f));
      _dot = ui.Capsule("Handle", Rect, 5.5f, 5.5f, 13, 13, Color.white);
      SettingsPointerArea area = ui.Rect("Input", Rect, 0, 0, GripSize, GripSize).gameObject.AddComponent<SettingsPointerArea>();
      area.raycastTarget = true;
      area.Down = data => owner.GripDown(index, data);
      area.Dragged = data => owner.GripDrag(index, data);
      area.Up = _ => owner.EndDrag();
      SettingsCursor.Attach(area.gameObject, SettingsCursorKind.Pointer);
    }

    public RectTransform Rect { get; }

    public void Paint(bool stuck, bool small)
    {
      float size = small ? 10 : 13;
      SettingsUiFactory.Place(_dot.rectTransform, (GripSize - size) * .5f, (GripSize - size) * .5f, size, size);
      SettingsUiFactory.Place(_ring.rectTransform, (GripSize - size - 2) * .5f, (GripSize - size - 2) * .5f, size + 2, size + 2);
      _dot.color = stuck ? SettingsPalette.SoftAccent : Color.white;
    }
  }

  private sealed class Square
  {
    public Square(SettingsUiFactory ui, Transform parent, OverlayGradientHandles owner)
    {
      Rect = ui.Rect("Gradient color", parent, 0, 0, SquareSize, SquareSize);
      Graphic = Rect.gameObject.AddComponent<SettingsSwatchGraphic>();
      Graphic.raycastTarget = false;
      Graphic.Radius = 5;
      Graphic.Ring = 3;
      SettingsPointerArea area = ui.Rect("Input", Rect, 0, 0, SquareSize, SquareSize).gameObject.AddComponent<SettingsPointerArea>();
      area.raycastTarget = true;
      area.Down = data => owner.SquareDown(Stop, data);
      area.Dragged = owner.SquareDrag;
      area.Up = _ => owner.EndDrag();
      SettingsCursor.Attach(area.gameObject, SettingsCursorKind.Pointer);
    }

    public RectTransform Rect { get; }
    public SettingsSwatchGraphic Graphic { get; }
    public int Stop;
  }
}

/// <summary>Thin lines and dots in frame units (y down), for the gradient handles.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class OverlayGradientLines : MaskableGraphic
{
  private readonly List<(Vector2 A, Vector2 B, Color Color, float Width)> _segments = new();
  private readonly List<(Vector2 At, float Radius, Color Color)> _dots = new();
  private Rect _rect;

  public void Begin(Rect rect)
  {
    _rect = rect;
    _segments.Clear();
    _dots.Clear();
  }

  public void Segment(Vector2 a, Vector2 b, Color color, float width)
  {
    // A dark edge under each white line keeps it readable on light colors.
    _segments.Add((a, b, new Color(0, 0, 0, color.a * .5f), width + 2));
    _segments.Add((a, b, color, width));
  }

  /// <summary>A closed outline; dashed in short pieces.</summary>
  public void Outline(List<Vector2> points, Color color, float width, bool dashed)
  {
    int count = points.Count;
    for (int i = 0; i < count; i++)
    {
      Vector2 a = points[i],
        b = points[(i + 1) % count];
      if (!dashed)
      {
        Segment(a, b, color, width);
        continue;
      }
      float length = (b - a).magnitude;
      for (float t = 0; t < length; t += 10)
        Segment(Vector2.Lerp(a, b, t / length), Vector2.Lerp(a, b, Mathf.Min(1, (t + 5) / length)), color, width);
    }
  }

  public void Dot(Vector2 at, float radius, Color color)
  {
    _dots.Add((at, radius + 1, new Color(0, 0, 0, .5f)));
    _dots.Add((at, radius, color));
  }

  public void End() => SetVerticesDirty();

  private Vector2 Local(Vector2 frame) => new(_rect.xMin + frame.x, _rect.yMax - frame.y);

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    foreach ((Vector2 a, Vector2 b, Color color, float width) in _segments)
    {
      Vector2 from = Local(a),
        to = Local(b);
      Vector2 along = to - from;
      if (along.sqrMagnitude < 1e-6f)
        continue;
      Vector2 normal = new Vector2(-along.y, along.x).normalized;
      float inner = Mathf.Max(.05f, width * .5f - .4f),
        outer = width * .5f + .4f;
      Color clear = color;
      clear.a = 0;
      int start = mesh.currentVertCount;
      mesh.AddVert(from + normal * outer, clear, Vector2.zero);
      mesh.AddVert(from + normal * inner, color, Vector2.zero);
      mesh.AddVert(from - normal * inner, color, Vector2.zero);
      mesh.AddVert(from - normal * outer, clear, Vector2.zero);
      mesh.AddVert(to + normal * outer, clear, Vector2.zero);
      mesh.AddVert(to + normal * inner, color, Vector2.zero);
      mesh.AddVert(to - normal * inner, color, Vector2.zero);
      mesh.AddVert(to - normal * outer, clear, Vector2.zero);
      for (int strip = 0; strip < 3; strip++)
      {
        mesh.AddTriangle(start + strip, start + 4 + strip, start + 5 + strip);
        mesh.AddTriangle(start + strip, start + 5 + strip, start + 1 + strip);
      }
    }
    foreach ((Vector2 at, float radius, Color color) in _dots)
    {
      Vector2 center = Local(at);
      Color clear = color;
      clear.a = 0;
      int start = mesh.currentVertCount;
      mesh.AddVert(center, color, Vector2.zero);
      const int segments = 16;
      for (int i = 0; i <= segments; i++)
      {
        float angle = i * Mathf.PI * 2 / segments;
        Vector2 n = new(Mathf.Cos(angle), Mathf.Sin(angle));
        mesh.AddVert(center + n * (radius - .4f), color, Vector2.zero);
        mesh.AddVert(center + n * (radius + .4f), clear, Vector2.zero);
        if (i == 0)
          continue;
        int p = start + 1 + (i - 1) * 2,
          q = p + 2;
        mesh.AddTriangle(start, p, q);
        mesh.AddTriangle(p, p + 1, q + 1);
        mesh.AddTriangle(p, q + 1, q);
      }
    }
  }
}
