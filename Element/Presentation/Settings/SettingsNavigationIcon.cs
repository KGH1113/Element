using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed class SettingsNavigationIcon : SettingsVectorGraphic
{
  public int Kind;
  protected override float DesignSize => 32;

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (Kind)
    {
      case 0:
        BeginPath();
        PathPoint(5, 6);
        PathPoint(27, 6);
        PathPoint(27, 26);
        PathPoint(5, 26);
        Stroke(mesh, 2, true);
        Line(mesh, 14, 6, 14, 26);
        Line(mesh, 14, 16, 27, 16);
        break;
      case 1:
        Line(mesh, 5, 8, 27, 8);
        Line(mesh, 5, 16, 27, 16);
        Line(mesh, 5, 24, 27, 24);
        Dot(mesh, 11, 8, 3.2f);
        Dot(mesh, 23, 16, 3.2f);
        Dot(mesh, 15, 24, 3.2f);
        break;
      case 2:
        // An uninterrupted gear contour stays legible at a small display scale.
        BeginPath();
        for (int i = 0; i < 32; i++)
        {
          float angle = i * UnityEngine.Mathf.PI / 16;
          float radius = i % 4 == 1 || i % 4 == 2 ? 12 : 9;
          PathPoint(16 + UnityEngine.Mathf.Cos(angle) * radius, 16 + UnityEngine.Mathf.Sin(angle) * radius);
        }
        Stroke(mesh, 2, true);
        Arc(mesh, 16, 16, 3.8f, 0, 360);
        break;
      case 3:
      case 4:
        BeginPath();
        PathPoint(Kind == 4 ? 22 : 10, 5);
        PathPoint(Kind == 4 ? 11 : 21, 16);
        PathPoint(Kind == 4 ? 22 : 10, 27);
        Stroke(mesh, Kind == 4 ? 4 : 2.8f);
        break;
      case 5:
        Line(mesh, 8, 8, 24, 24, 3.2f);
        Line(mesh, 24, 8, 8, 24, 3.2f);
        break;
      case 6:
        BeginPath();
        PathPoint(16, 4);
        PathPoint(19.5f, 12.5f);
        PathPoint(28, 16);
        PathPoint(19.5f, 19.5f);
        PathPoint(16, 28);
        PathPoint(12.5f, 19.5f);
        PathPoint(4, 16);
        PathPoint(12.5f, 12.5f);
        Stroke(mesh, 2, true);
        Line(mesh, 25, 3, 25, 9);
        Line(mesh, 22, 6, 28, 6);
        break;
    }
  }
}
