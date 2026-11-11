using System;
using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Shape;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;
using NVector2 = System.Numerics.Vector2;

namespace Element.Presentation.Layout.ContentEditors.Shape;

/// <summary>
/// The numbers around the shape: W under it, H beside it, and an arc outside
/// each corner that opens the corner's rounding next to it.
/// </summary>
internal sealed class ShapeMeasures
{
  /// <summary>Between the shape and W or H, in frame units.</summary>
  public const float Gap = 22;

  public const string WidthKey = "width",
    HeightKey = "height";

  private const float RadiusOffset = 48;

  private readonly List<OverlayFormulaField> _radiusFields = new();
  private readonly List<string> _radiusKeys = new();
  private readonly List<Button> _arcs = new();
  private readonly List<NVector2> _corners = new(4);
  private OverlayFormulaField _widthField,
    _heightField;

  public OverlayFormulaField WidthField => _widthField;

  public OverlayFormulaField HeightField => _heightField;

  public IReadOnlyList<OverlayFormulaField> RadiusFields => _radiusFields;

  public IReadOnlyList<Button> Arcs => _arcs;

  /// <summary>The key of corner <paramref name="index"/>'s rounding.</summary>
  public string RadiusKey(int index) => _radiusKeys[index];

  public void Build(OverlayEditorFrame screen, OverlayShapeKind kind, Action<string> activate, bool reduceMotion)
  {
    _radiusFields.Clear();
    _radiusKeys.Clear();
    _arcs.Clear();
    _widthField = Field(screen, "Width", WidthKey, "W", activate, reduceMotion);
    _heightField = Field(screen, "Height", HeightKey, "H", activate, reduceMotion);
    int corners = OverlayShapeGeometry.DefaultPoints(kind).Count;
    for (int i = 0; i < corners; i++)
    {
      string key = "radius:" + i;
      _radiusKeys.Add(key);
      _arcs.Add(
        OverlayCornerArc.Create(
          screen.Ui,
          screen.Frame,
          L("꼭짓점 둥글기 ", "Corner rounding ") + (i + 1),
          () => activate(key),
          reduceMotion
        )
      );
      _radiusFields.Add(
        Field(screen, L("꼭짓점 둥글기 값 ", "Corner radius ") + (i + 1), key, "R", activate, reduceMotion)
      );
    }
  }

  private static OverlayFormulaField Field(
    OverlayEditorFrame screen,
    string name,
    string key,
    string prefix,
    Action<string> activate,
    bool reduceMotion
  ) => new(screen.Ui, screen.Frame, name, "px", () => activate(key), reduceMotion, prefix);

  /// <summary>
  /// W under the shape and H beside it; an arc outside each corner, with the
  /// corner's rounding shown while it is edited. Gradient handles may sit on
  /// a corner, so the arcs step aside while they show.
  /// </summary>
  public void LayOut(ShapePreview preview, ShapeNumbers numbers, bool corners, bool handles, bool reduceMotion)
  {
    OverlayShapeSettings resolved = preview.Resolved;
    float left = preview.TopLeft.x,
      top = preview.TopLeft.y;
    float boxWidth = resolved.Width * preview.UnitsToFrame,
      boxHeight = resolved.Height * preview.UnitsToFrame;
    numbers.Show(_widthField, WidthKey);
    _widthField.Place(left + boxWidth * .5f - _widthField.Width * .5f, top + boxHeight + Gap);
    numbers.Show(_heightField, HeightKey);
    _heightField.Place(left + boxWidth + Gap, top + boxHeight * .5f - OverlayFormulaField.Height * .5f);

    OverlayShapeGeometry.Corners(resolved, _corners);
    for (int i = 0; i < _arcs.Count; i++)
    {
      bool corner = corners && i < _corners.Count && !handles;
      string key = _radiusKeys[i];
      bool editing = corner && numbers.Field == key;
      _arcs[i].gameObject.SetActive(corner);
      _radiusFields[i].SetVisible(editing);
      if (!corner)
        continue;
      Vector2 point = preview.Frame(_corners[i]);
      Vector2 outward = Outward(i);
      RectTransform arc = (RectTransform)_arcs[i].transform;
      UiFactory.Place(
        arc,
        point.x - OverlayCornerArc.Size * .5f,
        point.y - OverlayCornerArc.Size * .5f,
        OverlayCornerArc.Size,
        OverlayCornerArc.Size
      );
      arc.GetComponent<OverlayCornerArc>().Angle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
      OverlayCornerArc.Paint(_arcs[i], editing, reduceMotion);
      if (!editing)
        continue;
      OverlayFormulaField radius = _radiusFields[i];
      numbers.Show(radius, key);
      Vector2 center = point + outward * RadiusOffset;
      radius.Place(center.x - radius.Width * .5f, center.y - OverlayFormulaField.Height * .5f);
      radius.Root.SetAsLastSibling();
    }
  }

  /// <summary>The corner's arc whose screen point is over it, or -1.</summary>
  public int ArcAt(Vector2 screen, bool shownOnly)
  {
    for (int i = 0; i < _arcs.Count; i++)
      if (
        (!shownOnly || _arcs[i].gameObject.activeSelf)
        && PopoverPlacement.Contains((RectTransform)_arcs[i].transform, screen)
      )
        return i;
    return -1;
  }

  // Away from the shape across the corner (screen y down).
  private Vector2 Outward(int index)
  {
    int count = _corners.Count;
    NVector2 vertex = _corners[index];
    NVector2 toPrevious = NVector2.Normalize(_corners[(index + count - 1) % count] - vertex);
    NVector2 toNext = NVector2.Normalize(_corners[(index + 1) % count] - vertex);
    NVector2 inward = toPrevious + toNext;
    Vector2 direction = inward.LengthSquared() > 1e-6f ? -new Vector2(inward.X, inward.Y).normalized : Vector2.up;
    // Corners moved in 1.1.0 can point inward; keep the arc outside the middle.
    NVector2 center = NVector2.Zero;
    foreach (NVector2 corner in _corners)
      center += corner;
    center /= count;
    Vector2 away = new(vertex.X - center.X, vertex.Y - center.Y);
    if (Vector2.Dot(direction, away) < 0)
      direction = -direction;
    return direction;
  }
}
