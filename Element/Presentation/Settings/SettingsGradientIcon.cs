using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed class SettingsGradientIcon : SettingsVectorGraphic
{
  public SettingsGradientIconKind Kind;

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (Kind)
    {
      case SettingsGradientIconKind.Flip:
        // Two arrows passing each other.
        Line(mesh, 4, 8.5f, 19, 8.5f);
        Line(mesh, 15.5f, 5, 19, 8.5f);
        Line(mesh, 15.5f, 12, 19, 8.5f);
        Line(mesh, 20, 15.5f, 5, 15.5f);
        Line(mesh, 8.5f, 12, 5, 15.5f);
        Line(mesh, 8.5f, 19, 5, 15.5f);
        break;
      case SettingsGradientIconKind.Rotate:
        // Most of a circle with an arrow on its end.
        Arc(mesh, 12, 12.5f, 7, -60, 220);
        Line(mesh, 15.5f, 6.4f, 19.6f, 6.4f);
        Line(mesh, 19.6f, 2.3f, 19.6f, 6.4f);
        break;
      default:
        // A bin with its lid.
        Line(mesh, 4.5f, 7, 19.5f, 7);
        Line(mesh, 9.5f, 7, 9.5f, 4.5f);
        Line(mesh, 9.5f, 4.5f, 14.5f, 4.5f);
        Line(mesh, 14.5f, 4.5f, 14.5f, 7);
        Line(mesh, 6.5f, 7, 7.5f, 19.5f);
        Line(mesh, 7.5f, 19.5f, 16.5f, 19.5f);
        Line(mesh, 16.5f, 19.5f, 17.5f, 7);
        break;
    }
  }
}
