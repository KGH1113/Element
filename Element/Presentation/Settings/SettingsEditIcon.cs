using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed class SettingsEditIcon : SettingsVectorGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    BeginPath();
    PathPoint(4, 16);
    PathPoint(15, 5);
    PathPoint(19, 9);
    PathPoint(8, 20);
    PathPoint(3, 21);
    PathPoint(4, 16);
    Stroke(mesh);
    Line(mesh, 12, 8, 16, 12);
  }
}
