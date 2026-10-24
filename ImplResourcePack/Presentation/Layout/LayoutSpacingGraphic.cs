using ImplResourcePack.Domain.Layout;
using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Layout;

// Paired dimension lines show exactly which two empty gaps are equal.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LayoutSpacingGraphic : MaskableGraphic
{
  private LayoutSpacingGuide _horizontal,
    _vertical;
  private float _scale = 1;

  public void SetGuides(LayoutSpacingGuide horizontal, LayoutSpacingGuide vertical, float scale)
  {
    _horizontal = horizontal;
    _vertical = vertical;
    _scale = scale;
    bool visible = horizontal.Visible || vertical.Visible;
    if (gameObject.activeSelf != visible)
      gameObject.SetActive(visible);
    if (visible)
      SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Draw(mesh, _horizontal, false);
    Draw(mesh, _vertical, true);
  }

  private void Draw(VertexHelper mesh, LayoutSpacingGuide guide, bool vertical)
  {
    if (!guide.Visible)
      return;
    Gap(mesh, guide.Start1, guide.End1, guide.Cross, vertical);
    Gap(mesh, guide.Start2, guide.End2, guide.Cross, vertical);
  }

  private void Gap(VertexHelper mesh, float start, float end, float cross, bool vertical)
  {
    float half = _scale,
      cap = 5 * _scale;
    Box(mesh, start, cross - half, end - start, half * 2, vertical);
    Box(mesh, start - half, cross - cap, half * 2, cap * 2, vertical);
    Box(mesh, end - half, cross - cap, half * 2, cap * 2, vertical);
    // A double tick identifies equality without relying on color or text.
    float center = (start + end) * .5f;
    if (end - start > 14 * _scale)
    {
      Box(mesh, center - 3 * _scale, cross - 3 * _scale, _scale, 6 * _scale, vertical);
      Box(mesh, center + 2 * _scale, cross - 3 * _scale, _scale, 6 * _scale, vertical);
    }
  }

  private void Box(VertexHelper mesh, float x, float y, float width, float height, bool vertical)
  {
    if (vertical)
    {
      (x, y) = (y, x);
      (width, height) = (height, width);
    }
    float left = rectTransform.rect.xMin + x,
      top = rectTransform.rect.yMax - y;
    int index = mesh.currentVertCount;
    mesh.AddVert(new Vector3(left, top), color, Vector2.zero);
    mesh.AddVert(new Vector3(left + width, top), color, Vector2.zero);
    mesh.AddVert(new Vector3(left + width, top - height), color, Vector2.zero);
    mesh.AddVert(new Vector3(left, top - height), color, Vector2.zero);
    mesh.AddTriangle(index, index + 1, index + 2);
    mesh.AddTriangle(index, index + 2, index + 3);
  }
}
