using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>Export: an arrow out of a tray, like the system's share icon.</summary>
internal sealed class SettingsExportIcon : SettingsVectorGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    BeginPath();
    PathPoint(8, 10);
    PathPoint(5, 10);
    PathPoint(5, 21);
    PathPoint(19, 21);
    PathPoint(19, 10);
    PathPoint(16, 10);
    Stroke(mesh);
    Line(mesh, 12, 3, 12, 15);
    BeginPath();
    PathPoint(8, 7);
    PathPoint(12, 3);
    PathPoint(16, 7);
    Stroke(mesh);
  }
}
