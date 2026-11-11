using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class UiAlphaGraphic : MaskableGraphic
{
  private Color _value;

  public void SetColor(Color color)
  {
    if (_value == color)
      return;
    _value = color;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect rect = rectTransform.rect;
    const float cell = 6;
    int columns = Mathf.CeilToInt(rect.width / cell),
      rows = Mathf.CeilToInt(rect.height / cell);
    for (int y = 0; y < rows; y++)
    for (int x = 0; x < columns; x++)
    {
      float left = x * cell,
        right = Mathf.Min(rect.width, left + cell);
      float bottom = y * cell,
        top = Mathf.Min(rect.height, bottom + cell);
      Color checker = (x + y) % 2 == 0 ? new Color(.30f, .30f, .30f) : new Color(.48f, .48f, .48f);
      Color startColor = Color.Lerp(checker, _value, left / rect.width);
      Color endColor = Color.Lerp(checker, _value, right / rect.width);
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector2(rect.xMin + left, rect.yMin + bottom), startColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + left, rect.yMin + top), startColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + right, rect.yMin + top), endColor, Vector2.zero);
      mesh.AddVert(new Vector2(rect.xMin + right, rect.yMin + bottom), endColor, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
  }
}
