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
  Text,
  KeyViewer,
  Shape,
  Square,
  Triangle,
  Circle,
  Backspace,
  Back,
  Forward,
  GrowFrom,
  StrokeWidth,
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
      case LayoutAction.Text:
        Line(mesh, 5, 5, 19, 5);
        Line(mesh, 12, 5, 12, 20);
        Line(mesh, 9, 20, 15, 20);
        break;
      case LayoutAction.KeyViewer:
        Box(mesh, 3, 5, 10, 12);
        Box(mesh, 14, 5, 21, 12);
        Box(mesh, 3, 15, 21, 20);
        break;
      case LayoutAction.Shape:
        Box(mesh, 3, 3, 14, 14);
        Arc(mesh, 15.5f, 15.5f, 5.5f, 0, 360);
        break;
      case LayoutAction.Square:
        Box(mesh, 5, 5, 19, 19);
        break;
      case LayoutAction.Triangle:
        BeginPath();
        PathPoint(12, 4);
        PathPoint(20.5f, 19);
        PathPoint(3.5f, 19);
        Stroke(mesh, 2, true);
        break;
      case LayoutAction.Circle:
        Arc(mesh, 12, 12, 8, 0, 360);
        break;
      case LayoutAction.Back:
        Line(mesh, 15, 5, 8, 12);
        Line(mesh, 8, 12, 15, 19);
        break;
      case LayoutAction.Forward:
        Line(mesh, 9, 5, 16, 12);
        Line(mesh, 16, 12, 9, 19);
        break;
      case LayoutAction.GrowFrom:
        // A box with its top-left point filled and arrows out of it.
        Box(mesh, 4, 4, 20, 20);
        Dot(mesh, 4, 4, 3);
        Line(mesh, 9, 9, 15, 15);
        Line(mesh, 15, 15, 15, 11);
        Line(mesh, 15, 15, 11, 15);
        break;
      case LayoutAction.StrokeWidth:
        // Lines growing heavier, like a border's width.
        Line(mesh, 4, 6, 20, 6, 1.2f);
        Line(mesh, 4, 12, 20, 12, 2.2f);
        Line(mesh, 4, 18.5f, 20, 18.5f, 3.6f);
        break;
      case LayoutAction.Backspace:
        BeginPath();
        PathPoint(9, 5);
        PathPoint(21, 5);
        PathPoint(21, 19);
        PathPoint(9, 19);
        PathPoint(3, 12);
        Stroke(mesh, 2, true);
        Line(mesh, 12, 9, 18, 15);
        Line(mesh, 18, 9, 12, 15);
        break;
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

  private void Box(VertexHelper mesh, float left, float top, float right, float bottom)
  {
    BeginPath();
    PathPoint(left, top);
    PathPoint(right, top);
    PathPoint(right, bottom);
    PathPoint(left, bottom);
    Stroke(mesh, 2, true);
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
