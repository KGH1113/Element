using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsGradientGraphic : MaskableGraphic
{
  public bool HueStrip;
  private float _hue = -1;

  public void SetHue(float hue)
  {
    if (_hue == hue)
      return;
    _hue = hue;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect r = rectTransform.rect;
    int segments = HueStrip ? 6 : 1;
    for (int i = 0; i < segments; i++)
    {
      Color left = HueStrip ? Color.HSVToRGB(i / 6f, 1, 1) : Color.white;
      Color right = Color.HSVToRGB(HueStrip ? (i + 1) / 6f : Mathf.Max(0, _hue), 1, 1);
      float x0 = r.xMin + r.width * i / segments;
      float x1 = r.xMin + r.width * (i + 1) / segments;
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector3(x0, r.yMin), left, Vector2.zero);
      mesh.AddVert(new Vector3(x0, r.yMax), left, Vector2.zero);
      mesh.AddVert(new Vector3(x1, r.yMax), right, Vector2.zero);
      mesh.AddVert(new Vector3(x1, r.yMin), right, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
    if (!HueStrip)
    {
      // Multiply the horizontal white-to-hue ramp by a vertical value ramp.
      // A single four-corner quad interpolates across its diagonal and is not HSV.
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector3(r.xMin, r.yMin), Color.black, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMin, r.yMax), Color.clear, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMax, r.yMax), Color.clear, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMax, r.yMin), Color.black, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
  }
}
