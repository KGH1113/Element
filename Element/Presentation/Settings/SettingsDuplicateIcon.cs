using UnityEngine.UI;

namespace Element.Presentation.Settings;

/// <summary>Duplicate: a second sheet behind the first.</summary>
internal sealed class SettingsDuplicateIcon : SettingsVectorGraphic
{
  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    BeginPath();
    PathPoint(4, 8);
    PathPoint(16, 8);
    PathPoint(16, 20);
    PathPoint(4, 20);
    Stroke(mesh, 2, true);
    BeginPath();
    PathPoint(8, 8);
    PathPoint(8, 4);
    PathPoint(20, 4);
    PathPoint(20, 16);
    PathPoint(16, 16);
    Stroke(mesh);
  }
}
