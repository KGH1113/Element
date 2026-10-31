using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

internal enum LayoutAction
{
  Undo,
  Redo,
  Reset,
  Snap,
  Plus,
  Minus,
  Grip,
  Delete,
  Close,
}

// Vector controls don't depend on Unicode coverage in the overlay font.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class LayoutActionIcon : SettingsVectorGraphic
{
  public LayoutAction Kind;

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (Kind)
    {
      case LayoutAction.Close:
        Line(mesh, 6, 6, 18, 18);
        Line(mesh, 18, 6, 6, 18);
        break;
      case LayoutAction.Delete:
        Line(mesh, 4, 6, 20, 6);
        Line(mesh, 9, 6, 9, 3);
        Line(mesh, 9, 3, 15, 3);
        Line(mesh, 15, 3, 15, 6);
        Line(mesh, 6, 6, 7, 21);
        Line(mesh, 7, 21, 17, 21);
        Line(mesh, 17, 21, 18, 6);
        Line(mesh, 10, 10, 10, 17);
        Line(mesh, 14, 10, 14, 17);
        break;
      case LayoutAction.Plus:
        Line(mesh, 12, 5, 12, 19);
        Line(mesh, 5, 12, 19, 12);
        break;
      case LayoutAction.Minus:
        Line(mesh, 5, 12, 19, 12);
        break;
      case LayoutAction.Grip:
        for (int x = 8; x <= 16; x += 8)
        for (int y = 4; y <= 20; y += 8)
          Dot(mesh, x, y, 1.4f);
        break;
      case LayoutAction.Undo:
      case LayoutAction.Redo:
        bool mirror = Kind == LayoutAction.Redo;
        Curve(mesh, new Vector2(4, 8), new Vector2(22, 3), new Vector2(24, 21), new Vector2(9, 19), mirror);
        Line(mesh, mirror ? 20 : 4, 3, mirror ? 20 : 4, 8);
        Line(mesh, mirror ? 20 : 4, 8, mirror ? 15 : 9, 9);
        break;
      case LayoutAction.Reset:
        Arc(mesh, 12, 12, 8, -130, 175);
        Line(mesh, 4, 7, 4, 12);
        Line(mesh, 4, 12, 9, 12);
        break;
      case LayoutAction.Snap:
        Line(mesh, 5, 4, 5, 12);
        Line(mesh, 19, 4, 19, 12);
        Arc(mesh, 12, 12, 7, 0, 180);
        Line(mesh, 5, 4, 9, 4);
        Line(mesh, 15, 4, 19, 4);
        Line(mesh, 9, 4, 9, 12);
        Line(mesh, 15, 4, 15, 12);
        Arc(mesh, 12, 12, 3, 0, 180);
        Line(mesh, 5, 8, 9, 8);
        Line(mesh, 15, 8, 19, 8);
        break;
    }
  }

  private void Curve(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, bool mirror)
  {
    BeginPath();
    for (int i = 0; i <= 24; i++)
    {
      float t = i / 24f,
        u = 1 - t;
      Vector2 p = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
      PathPoint(mirror ? 24 - p.x : p.x, p.y);
    }
    Stroke(mesh);
  }
}
