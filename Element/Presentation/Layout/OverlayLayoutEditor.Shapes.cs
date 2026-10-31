using System.Collections.Generic;
using Element.Domain.Elements;
using Element.Domain.Layout;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using UnityEngine;
using static Element.Presentation.Settings.SettingsLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Layout;

// A selected shape's corner and rounding handles, and exact values for any handle.
internal sealed partial class OverlayLayoutEditor
{
  private enum ShapeDrag
  {
    None,
    Corner,
    Radius,
  }

  private const float RadiusHandleOffset = 16;
  private ShapeDrag _shapeDrag;
  private int _shapeIndex;
  private ShapeHandlesGraphic _shapeHandles;
  private LayoutValuePopover _valuePopover;
  private readonly List<NVector2> _shapeStartCorners = new(4);
  private readonly List<Vector2> _handleCorners = new(4);
  private readonly List<Vector2> _handleRadii = new(4);
  private Vector2 _shapeStartTopLeft;
  private float _shapeStartPixels = 1;

  private ShapeOverlayElement SelectedShape => HasSelection ? _frames[_selected].Target as ShapeOverlayElement : null;

  private bool TryShapeHandleAt(Vector2 mouse, out ShapeDrag kind, out int index)
  {
    kind = ShapeDrag.None;
    index = -1;
    ShapeOverlayElement shape = SelectedShape;
    if (shape?.Shape == null || !OverlayShapeGeometry.HasCorners(shape.Shape.Kind))
      return false;
    CollectHandles(shape);
    float corner = 8 * UiScale,
      radius = 7 * UiScale;
    for (int i = 0; i < _handleCorners.Count; i++)
      if ((_handleCorners[i] - mouse).sqrMagnitude <= corner * corner)
      {
        kind = ShapeDrag.Corner;
        index = i;
        return true;
      }
    for (int i = 0; i < _handleRadii.Count; i++)
      if (!float.IsNaN(_handleRadii[i].x) && (_handleRadii[i] - mouse).sqrMagnitude <= radius * radius)
      {
        kind = ShapeDrag.Radius;
        index = i;
        return true;
      }
    return false;
  }

  // Screen positions of every corner and its rounding handle (NaN for a straight corner).
  private void CollectHandles(ShapeOverlayElement shape)
  {
    _handleCorners.Clear();
    _handleRadii.Clear();
    IReadOnlyList<NVector2> corners = shape.Corners;
    float pixels = Mathf.Max(.0001f, shape.PixelsPerUnit);
    float offset = RadiusHandleOffset * UiScale / pixels;
    float apart = 6 * UiScale;
    for (int i = 0; i < corners.Count; i++)
    {
      Vector2 corner = shape.BoxToScreen(corners[i]);
      Vector2 radius = new(float.NaN, float.NaN);
      if (OverlayShapeGeometry.TryRadiusHandle(corners, i, shape.Shape.Points[i].Radius, offset, out NVector2 handle))
        radius = shape.BoxToScreen(handle);
      // Corners too small to show both handles apart keep only the corner.
      if ((radius - corner).sqrMagnitude < apart * apart)
        radius = new Vector2(float.NaN, float.NaN);
      _handleCorners.Add(corner);
      _handleRadii.Add(radius);
    }
  }

  private void BeginShapeDrag(ShapeDrag kind, int index, Vector2 mouse)
  {
    ShapeOverlayElement shape = SelectedShape;
    if (shape == null)
      return;
    _scalePending = false;
    _scaleErrorUntil = 0;
    _dragging = _selected;
    _dragHandle = LayoutHandle.None;
    _shapeDrag = kind;
    _shapeIndex = index;
    _dragStart = shape.Bounds;
    _pressPosition = mouse;
    _pressMoved = false;
    _shapeStartCorners.Clear();
    _shapeStartCorners.AddRange(shape.Corners);
    _shapeStartPixels = Mathf.Max(.0001f, shape.PixelsPerUnit);
    _shapeStartTopLeft = shape.BoxToScreen(NVector2.Zero);
  }

  private void DragShapeHandle(Vector2 mouse)
  {
    if (!_pressMoved || _frames[_dragging].Target is not ShapeOverlayElement shape || shape.Shape == null)
      return;
    Vector2 local = (mouse - _shapeStartTopLeft) / _shapeStartPixels;
    NVector2 point = new(local.x, local.y);
    if (_shapeDrag == ShapeDrag.Corner)
    {
      OverlayShapeGeometry.MoveCorner(shape.Shape, _shapeStartCorners, _shapeIndex, point, out NVector2 origin);
      shape.SetBox(
        shape.Shape.Width,
        shape.Shape.Height,
        _shapeStartTopLeft + new Vector2(origin.X, origin.Y) * _shapeStartPixels,
        _shapeStartPixels
      );
    }
    else if (_shapeDrag == ShapeDrag.Radius)
    {
      float offset = RadiusHandleOffset * UiScale / _shapeStartPixels;
      shape.Shape.Points[_shapeIndex].Radius = OverlayShapeGeometry.RadiusFromHandle(
        _shapeStartCorners,
        _shapeIndex,
        point,
        offset
      );
      shape.Redraw();
    }
  }

  private void UpdateShapeHandles(bool covered)
  {
    if (_shapeHandles == null)
    {
      RectTransform rect = _ui.Rect("Shape handles", _root.transform, 0, 0, ViewWidth, ViewHeight);
      SettingsUiFactory.Stretch(rect, 0, 0, 0, 0);
      _shapeHandles = rect.gameObject.AddComponent<ShapeHandlesGraphic>();
      _shapeHandles.raycastTarget = false;
    }
    ShapeOverlayElement shape = SelectedShape;
    bool visible =
      !covered
      && shape?.Shape != null
      && OverlayShapeGeometry.HasCorners(shape.Shape.Kind)
      && !(_dragging >= 0 && _shapeDrag == ShapeDrag.None && _dragHandle == LayoutHandle.Move && _pressMoved);
    if (_shapeHandles.gameObject.activeSelf != visible)
      _shapeHandles.gameObject.SetActive(visible);
    if (!visible)
      return;
    CollectHandles(shape);
    _shapeHandles.color = SettingsPalette.SoftAccent;
    _shapeHandles.Set(
      _handleCorners,
      _handleRadii,
      UiScale,
      _dragging >= 0 ? _shapeIndex : -1,
      _shapeDrag == ShapeDrag.Radius
    );
    _shapeHandles.transform.SetAsLastSibling();
    _toolbar.SetAsLastSibling();
  }

  private void OpenShapeValues(ShapeOverlayElement shape, ShapeDrag handle, int index, LayoutHandle boxHandle)
  {
    if (shape.Shape == null)
      return;
    ClosePopovers();
    _valuePopover ??= new LayoutValuePopover(_root.transform, _font, () => _preferences.ReduceUiMotion);
    float scale = Mathf.Max(.0001f, shape.EffectiveScale);
    Vector2 anchor;
    if (handle == ShapeDrag.Corner)
    {
      NVector2 corner = shape.Corners[index];
      anchor = shape.BoxToScreen(corner);
      _valuePopover.Show(
        L("꼭짓점 ", "Corner ") + (index + 1),
        new[]
        {
          new LayoutValuePopover.Field("X", corner.X * scale, -10000, 10000),
          new LayoutValuePopover.Field("Y", corner.Y * scale, -10000, 10000),
        },
        values => ApplyCorner(shape, index, new NVector2(values[0] / scale, values[1] / scale))
      );
    }
    else if (handle == ShapeDrag.Radius)
    {
      IReadOnlyList<NVector2> corners = shape.Corners;
      anchor = shape.BoxToScreen(corners[index]);
      float max = OverlayShapeGeometry.MaxRadius(corners, index) * scale;
      _valuePopover.Show(
        L("꼭짓점 ", "Corner ") + (index + 1) + L(" 둥글기", " radius"),
        new[]
        {
          new LayoutValuePopover.Field(
            L("반지름", "Radius"),
            OverlayShapeGeometry.ClampRadius(corners, index, shape.Shape.Points[index].Radius) * scale,
            0,
            Mathf.Floor(max * 10) / 10
          ),
        },
        values =>
        {
          shape.Shape.Points[index].Radius = values[0] / scale;
          shape.Redraw();
          _history.Commit(_host.LayoutSettings);
          return null;
        }
      );
    }
    else
    {
      anchor = HandlePoint(EditBounds(shape), boxHandle);
      float width = shape.Shape.Width * scale,
        height = shape.Shape.Height * scale;
      if (IsEdge(boxHandle))
      {
        bool horizontal = boxHandle == LayoutHandle.Left || boxHandle == LayoutHandle.Right;
        _valuePopover.Show(
          horizontal ? L("너비", "Width") : L("높이", "Height"),
          new[]
          {
            new LayoutValuePopover.Field(
              horizontal ? L("너비", "Width") : L("높이", "Height"),
              horizontal ? width : height,
              1,
              10000
            ),
          },
          values => ApplyStretch(shape, boxHandle, values[0] / scale)
        );
      }
      else
        _valuePopover.Show(
          L("크기", "Size"),
          new[]
          {
            new LayoutValuePopover.Field(L("너비", "Width"), width, 1, 10000),
            new LayoutValuePopover.Field(L("높이", "Height"), height, 1, 10000),
          },
          values => ApplyCornerScale(shape, boxHandle, values[0] / width),
          width / Mathf.Max(.0001f, height)
        );
    }
    float popupScale = PopupScale(LayoutValuePopover.Width, _valuePopover.Height);
    float w = LayoutValuePopover.Width * popupScale,
      h = _valuePopover.Height * popupScale;
    float x = anchor.x + 16,
      y = anchor.y + 16;
    if (x + w > ViewWidth - 8)
      x = anchor.x - w - 16;
    if (y + h > ViewHeight - 8)
      y = anchor.y - h - 16;
    PlacePopover(
      _valuePopover.Root,
      new Rect(
        Mathf.Clamp(x, 8, Mathf.Max(8, ViewWidth - w - 8)),
        Mathf.Clamp(y, 8, Mathf.Max(8, ViewHeight - h - 8)),
        w,
        h
      ),
      LayoutValuePopover.Width,
      _valuePopover.Height
    );
  }

  private string ApplyCorner(ShapeOverlayElement shape, int index, NVector2 position)
  {
    List<NVector2> corners = new(shape.Corners);
    float pixels = Mathf.Max(.0001f, shape.PixelsPerUnit);
    Vector2 topLeft = shape.BoxToScreen(NVector2.Zero);
    OverlayShapeGeometry.MoveCorner(shape.Shape, corners, index, position, out NVector2 origin);
    shape.SetBox(shape.Shape.Width, shape.Shape.Height, topLeft + new Vector2(origin.X, origin.Y) * pixels, pixels);
    shape.CommitShape();
    _history.Commit(_host.LayoutSettings);
    return null;
  }

  private string ApplyStretch(ShapeOverlayElement shape, LayoutHandle handle, float size)
  {
    float pixels = Mathf.Max(.0001f, shape.PixelsPerUnit);
    Rect bounds = shape.Bounds;
    float length = size * pixels;
    Rect target = handle switch
    {
      LayoutHandle.Left => new Rect(bounds.xMax - length, bounds.y, length, bounds.height),
      LayoutHandle.Right => new Rect(bounds.x, bounds.y, length, bounds.height),
      LayoutHandle.Top => new Rect(bounds.x, bounds.yMax - length, bounds.width, length),
      _ => new Rect(bounds.x, bounds.y, bounds.width, length),
    };
    shape.SetStretchedBounds(target, pixels);
    shape.CommitShape();
    _history.Commit(_host.LayoutSettings);
    return null;
  }

  private string ApplyCornerScale(ShapeOverlayElement shape, LayoutHandle handle, float ratio)
  {
    float scale = shape.EffectiveScale * ratio;
    if (scale < .2f || scale > MaximumScale)
      return L("크기는 20%–1000% 사이로만 바꿀 수 있어요", "Size can change between 20% and 1000%");
    Rect bounds = shape.Bounds;
    Vector2 size = bounds.size * ratio;
    bool left = handle == LayoutHandle.TopLeft || handle == LayoutHandle.BottomLeft;
    bool top = handle == LayoutHandle.TopLeft || handle == LayoutHandle.TopRight;
    shape.SetBounds(
      new Rect(left ? bounds.xMax - size.x : bounds.x, top ? bounds.yMax - size.y : bounds.y, size.x, size.y)
    );
    shape.CommitLayout(false);
    _history.Commit(_host.LayoutSettings);
    return null;
  }

  private static Vector2 HandlePoint(Rect bounds, LayoutHandle handle) =>
    handle switch
    {
      LayoutHandle.Left => new Vector2(bounds.xMin, bounds.center.y),
      LayoutHandle.Right => new Vector2(bounds.xMax, bounds.center.y),
      LayoutHandle.Top => new Vector2(bounds.center.x, bounds.yMin),
      LayoutHandle.Bottom => new Vector2(bounds.center.x, bounds.yMax),
      LayoutHandle.TopLeft => bounds.min,
      LayoutHandle.TopRight => new Vector2(bounds.xMax, bounds.yMin),
      LayoutHandle.BottomLeft => new Vector2(bounds.xMin, bounds.yMax),
      _ => bounds.max,
    };
}
