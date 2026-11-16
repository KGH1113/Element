using Element.Presentation.Ui.Graphics;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// A stop on a value color's band: its color in a thin ring with a dark
/// hairline outside, on a soft shadow that grows when the stop is picked and
/// lifted. A see-through color
/// shows over the opaque surface under it. Hollow, it is only the ring: where
/// a click would add a stop.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class OverlayValueColorKnob : MaskableGraphic
{
  private static readonly Color Shadow = new(0, 0, 0, .5f),
    Edge = new(0, 0, 0, .45f);

  private Color _fill = Color.white,
    _ring = Color.white,
    _under = Color.black;
  private float _radius = 9,
    _ringWidth = 2;
  private bool _lifted,
    _hollow;

  /// <summary>The stop's color in <paramref name="ring"/>, <paramref name="radius"/> to the ring's outside.</summary>
  public void Set(Color fill, Color ring, Color under, float radius, float ringWidth, bool lifted)
  {
    if (
      !_hollow
      && _fill == fill
      && _ring == ring
      && _under == under
      && _radius == radius
      && _ringWidth == ringWidth
      && _lifted == lifted
    )
      return;
    _hollow = false;
    _fill = fill;
    _ring = ring;
    _under = under;
    _radius = radius;
    _ringWidth = ringWidth;
    _lifted = lifted;
    SetVerticesDirty();
  }

  /// <summary>Only a ring, <paramref name="radius"/> to its outside.</summary>
  public void SetHollow(Color ring, float radius)
  {
    if (_hollow && _ring == ring && _radius == radius)
      return;
    _hollow = true;
    _ring = ring;
    _radius = radius;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    if (rect.width <= 0 || rect.height <= 0)
      return;
    float feather = UiMeshShapes.Feather(this);
    Vector2 center = rect.center;
    if (_hollow)
    {
      UiMeshShapes.Ring(mesh, center, _radius, 1.5f, _ring, feather);
      return;
    }
    // Picked, it lifts: the shadow drops further and softens.
    Color shadow = Shadow;
    shadow.a = _lifted ? .55f : .4f;
    UiMeshShapes.Disc(
      mesh,
      center + new Vector2(0, _lifted ? -2.2f : -1f),
      _radius + (_lifted ? 1.5f : .8f),
      shadow,
      Mathf.Max(feather, _lifted ? 5 : 3)
    );
    // A dark hairline keeps the ring apart from a light band behind it.
    UiMeshShapes.Disc(mesh, center, _radius + feather, Edge, feather);
    UiMeshShapes.Disc(mesh, center, _radius, _ring, feather);
    float inner = _radius - _ringWidth;
    if (_fill.a < 1)
      UiMeshShapes.Disc(mesh, center, inner, _under, feather);
    UiMeshShapes.Disc(mesh, center, inner, _fill, feather);
  }
}
