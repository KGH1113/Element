using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Graphics;

// Vector controls don't depend on Unicode coverage in the overlay font.
/// <summary>One icon of <see cref="UiIconKind"/>, drawn as vector strokes in its graphic's color.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class UiIcon : UiVectorGraphic
{
  public UiIconKind Kind;

  // The settings window's category icons and page arrows are drawn on a larger grid.
  protected override float DesignSize =>
    Kind
      is UiIconKind.Overlay
        or UiIconKind.Tweaks
        or UiIconKind.General
        or UiIconKind.Effects
        or UiIconKind.PageForward
        or UiIconKind.PageBack
        or UiIconKind.WindowClose
      ? 32
      : 24;

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (Kind)
    {
      case UiIconKind.Text:
        Line(mesh, 5, 5, 19, 5);
        Line(mesh, 12, 5, 12, 20);
        Line(mesh, 9, 20, 15, 20);
        break;
      case UiIconKind.Font:
        // "Aa", the usual mark for a typeface.
        Line(mesh, 3, 20, 8.5f, 5);
        Line(mesh, 8.5f, 5, 14, 20);
        Line(mesh, 5.2f, 14.5f, 11.8f, 14.5f);
        Arc(mesh, 18, 16.5f, 3.2f, 0, 360);
        Line(mesh, 21.2f, 12.5f, 21.2f, 20);
        break;
      case UiIconKind.Animate:
        // A value with waves on both sides: it pulses.
        Dot(mesh, 12, 12, 3);
        Arc(mesh, 12, 12, 7, -40, 40);
        Arc(mesh, 12, 12, 7, 140, 220);
        Arc(mesh, 12, 12, 10.5f, -32, 32);
        Arc(mesh, 12, 12, 10.5f, 148, 212);
        break;
      case UiIconKind.Less:
        Line(mesh, 17, 6, 7, 12, 2.4f);
        Line(mesh, 7, 12, 17, 18, 2.4f);
        break;
      case UiIconKind.LessOrSame:
        Line(mesh, 17, 4, 7, 9.5f, 2.4f);
        Line(mesh, 7, 9.5f, 17, 15, 2.4f);
        Line(mesh, 7, 19.5f, 17, 19.5f, 2.4f);
        break;
      case UiIconKind.Same:
        Line(mesh, 6, 9, 18, 9, 2.4f);
        Line(mesh, 6, 15, 18, 15, 2.4f);
        break;
      case UiIconKind.NotSame:
        Line(mesh, 6, 9, 18, 9, 2.4f);
        Line(mesh, 6, 15, 18, 15, 2.4f);
        Line(mesh, 15.5f, 4.5f, 8.5f, 19.5f, 2.4f);
        break;
      case UiIconKind.MoreOrSame:
        Line(mesh, 7, 4, 17, 9.5f, 2.4f);
        Line(mesh, 17, 9.5f, 7, 15, 2.4f);
        Line(mesh, 7, 19.5f, 17, 19.5f, 2.4f);
        break;
      case UiIconKind.More:
        Line(mesh, 7, 6, 17, 12, 2.4f);
        Line(mesh, 17, 12, 7, 18, 2.4f);
        break;
      case UiIconKind.AnyType:
        // An asterisk of three strokes through the middle.
        Line(mesh, 12, 4.5f, 12, 19.5f, 2.6f);
        Line(mesh, 5.5f, 8.25f, 18.5f, 15.75f, 2.6f);
        Line(mesh, 5.5f, 15.75f, 18.5f, 8.25f, 2.6f);
        break;
      case UiIconKind.PlaceType:
        // A box with its middle point marked.
        Box(mesh, 5, 5, 19, 19);
        Dot(mesh, 12, 12, 2.6f);
        break;
      case UiIconKind.CompareType:
        // Less and more, back to back: something that compares.
        Line(mesh, 9.5f, 6.5f, 4, 12, 2.4f);
        Line(mesh, 4, 12, 9.5f, 17.5f, 2.4f);
        Line(mesh, 14.5f, 6.5f, 20, 12, 2.4f);
        Line(mesh, 20, 12, 14.5f, 17.5f, 2.4f);
        break;
      case UiIconKind.Keyboard:
        // Two rows of keys over a space bar.
        Box(mesh, 2.5f, 5.5f, 21.5f, 18.5f);
        for (int key = 0; key < 4; key++)
        {
          Dot(mesh, 6.75f + key * 3.5f, 9.25f, 1.1f);
          Dot(mesh, 6.75f + key * 3.5f, 12.25f, 1.1f);
        }
        Line(mesh, 8, 15.5f, 16, 15.5f, 1.8f);
        break;
      case UiIconKind.Fork:
        // A bar over two boxes, a line from it down to each.
        Line(mesh, 5, 5.5f, 19, 5.5f, 3.4f);
        Line(mesh, 12, 7.2f, 12, 10.5f, 1.5f);
        Line(mesh, 7, 10.5f, 17, 10.5f, 1.5f);
        Line(mesh, 7, 10.5f, 7, 13, 1.5f);
        Line(mesh, 17, 10.5f, 17, 13, 1.5f);
        Box(mesh, 3.5f, 13.5f, 10.5f, 20);
        Box(mesh, 13.5f, 13.5f, 20.5f, 20);
        break;
      case UiIconKind.KeyViewer:
        Box(mesh, 3, 5, 10, 12);
        Box(mesh, 14, 5, 21, 12);
        Box(mesh, 3, 15, 21, 20);
        break;
      case UiIconKind.Shape:
        Box(mesh, 3, 3, 14, 14);
        Arc(mesh, 15.5f, 15.5f, 5.5f, 0, 360);
        break;
      case UiIconKind.Square:
        Box(mesh, 5, 5, 19, 19);
        break;
      case UiIconKind.Triangle:
        BeginPath();
        PathPoint(12, 4);
        PathPoint(20.5f, 19);
        PathPoint(3.5f, 19);
        Stroke(mesh, 2, true);
        break;
      case UiIconKind.Circle:
        Arc(mesh, 12, 12, 8, 0, 360);
        break;
      case UiIconKind.Extension:
        // Three blocks and a fourth set apart: pieces another mod adds.
        Box(mesh, 3, 3, 10.5f, 10.5f);
        Box(mesh, 13.5f, 3, 21, 10.5f);
        Box(mesh, 3, 13.5f, 10.5f, 21);
        Arc(mesh, 17.25f, 17.25f, 3.75f, 0, 360);
        break;
      case UiIconKind.Back:
        Line(mesh, 15, 5, 8, 12);
        Line(mesh, 8, 12, 15, 19);
        break;
      case UiIconKind.Up:
        Line(mesh, 5, 15, 12, 8);
        Line(mesh, 12, 8, 19, 15);
        break;
      case UiIconKind.Down:
        Line(mesh, 5, 9, 12, 16);
        Line(mesh, 12, 16, 19, 9);
        break;
      case UiIconKind.Check:
        Line(mesh, 5, 12.5f, 10, 17.5f, 2.4f);
        Line(mesh, 10, 17.5f, 19.5f, 7, 2.4f);
        break;
      case UiIconKind.Forward:
        Line(mesh, 9, 5, 16, 12);
        Line(mesh, 16, 12, 9, 19);
        break;
      case UiIconKind.GrowFrom:
        // A box with its top-left point filled and arrows out of it.
        Box(mesh, 4, 4, 20, 20);
        Dot(mesh, 4, 4, 3);
        Line(mesh, 9, 9, 15, 15);
        Line(mesh, 15, 15, 15, 11);
        Line(mesh, 15, 15, 11, 15);
        break;
      case UiIconKind.StrokeWidth:
        // Lines growing heavier, like a border's width.
        Line(mesh, 4, 6, 20, 6, 1.2f);
        Line(mesh, 4, 12, 20, 12, 2.2f);
        Line(mesh, 4, 18.5f, 20, 18.5f, 3.6f);
        break;
      // Two bars lined up against a guide line.
      case UiIconKind.AlignLeft:
        Line(mesh, 4, 3, 4, 21);
        Line(mesh, 8, 8, 20, 8, 4);
        Line(mesh, 8, 16, 15, 16, 4);
        break;
      case UiIconKind.AlignCenter:
        Line(mesh, 12, 3, 12, 21);
        Line(mesh, 5, 8, 19, 8, 4);
        Line(mesh, 8, 16, 16, 16, 4);
        break;
      case UiIconKind.AlignRight:
        Line(mesh, 20, 3, 20, 21);
        Line(mesh, 4, 8, 16, 8, 4);
        Line(mesh, 9, 16, 16, 16, 4);
        break;
      case UiIconKind.AlignTop:
        Line(mesh, 3, 4, 21, 4);
        Line(mesh, 8, 8, 8, 20, 4);
        Line(mesh, 16, 8, 16, 15, 4);
        break;
      case UiIconKind.AlignMiddle:
        Line(mesh, 3, 12, 21, 12);
        Line(mesh, 8, 5, 8, 19, 4);
        Line(mesh, 16, 8, 16, 16, 4);
        break;
      case UiIconKind.AlignBottom:
        Line(mesh, 3, 20, 21, 20);
        Line(mesh, 8, 4, 8, 16, 4);
        Line(mesh, 16, 9, 16, 16, 4);
        break;
      // Two bars between two edges, with equal room around them.
      case UiIconKind.DistributeHorizontal:
        Line(mesh, 3, 4, 3, 20);
        Line(mesh, 21, 4, 21, 20);
        Line(mesh, 9, 7, 9, 17, 3.5f);
        Line(mesh, 15, 7, 15, 17, 3.5f);
        break;
      case UiIconKind.DistributeVertical:
        Line(mesh, 4, 3, 20, 3);
        Line(mesh, 4, 21, 20, 21);
        Line(mesh, 7, 9, 17, 9, 3.5f);
        Line(mesh, 7, 15, 17, 15, 3.5f);
        break;
      case UiIconKind.Backspace:
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
      case UiIconKind.Close:
        Line(mesh, 6, 6, 18, 18);
        Line(mesh, 18, 6, 6, 18);
        break;
      case UiIconKind.Search:
        Arc(mesh, 10.5f, 10.5f, 6.5f, 0, 360);
        Line(mesh, 15.3f, 15.3f, 20, 20);
        break;
      case UiIconKind.Delete:
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
      case UiIconKind.Plus:
        Line(mesh, 12, 5, 12, 19);
        Line(mesh, 5, 12, 19, 12);
        break;
      case UiIconKind.Minus:
        Line(mesh, 5, 12, 19, 12);
        break;
      case UiIconKind.Grip:
        for (int x = 8; x <= 16; x += 8)
        for (int y = 4; y <= 20; y += 8)
          Dot(mesh, x, y, 1.4f);
        break;
      case UiIconKind.Undo:
      case UiIconKind.Redo:
        bool mirror = Kind == UiIconKind.Redo;
        Curve(mesh, new Vector2(4, 8), new Vector2(22, 3), new Vector2(24, 21), new Vector2(9, 19), mirror);
        Line(mesh, mirror ? 20 : 4, 3, mirror ? 20 : 4, 8);
        Line(mesh, mirror ? 20 : 4, 8, mirror ? 15 : 9, 9);
        break;
      case UiIconKind.Reset:
        Arc(mesh, 12, 12, 8, -130, 175);
        Line(mesh, 4, 7, 4, 12);
        Line(mesh, 4, 12, 9, 12);
        break;
      case UiIconKind.Snap:
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
      case UiIconKind.Overlay:
        BeginPath();
        PathPoint(5, 6);
        PathPoint(27, 6);
        PathPoint(27, 26);
        PathPoint(5, 26);
        Stroke(mesh, 2, true);
        Line(mesh, 14, 6, 14, 26);
        Line(mesh, 14, 16, 27, 16);
        break;
      case UiIconKind.Tweaks:
        Line(mesh, 5, 8, 27, 8);
        Line(mesh, 5, 16, 27, 16);
        Line(mesh, 5, 24, 27, 24);
        Dot(mesh, 11, 8, 3.2f);
        Dot(mesh, 23, 16, 3.2f);
        Dot(mesh, 15, 24, 3.2f);
        break;
      case UiIconKind.General:
        // An uninterrupted gear contour stays legible at a small display scale.
        BeginPath();
        for (int i = 0; i < 32; i++)
        {
          float angle = i * Mathf.PI / 16;
          float radius = i % 4 == 1 || i % 4 == 2 ? 12 : 9;
          PathPoint(16 + Mathf.Cos(angle) * radius, 16 + Mathf.Sin(angle) * radius);
        }
        Stroke(mesh, 2, true);
        Arc(mesh, 16, 16, 3.8f, 0, 360);
        break;
      case UiIconKind.PageForward:
      case UiIconKind.PageBack:
        BeginPath();
        bool back = Kind == UiIconKind.PageBack;
        PathPoint(back ? 22 : 10, 5);
        PathPoint(back ? 11 : 21, 16);
        PathPoint(back ? 22 : 10, 27);
        Stroke(mesh, back ? 4 : 2.8f);
        break;
      case UiIconKind.WindowClose:
        Line(mesh, 8, 8, 24, 24, 3.2f);
        Line(mesh, 24, 8, 8, 24, 3.2f);
        break;
      case UiIconKind.Effects:
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
      case UiIconKind.Flip:
        // Two arrows passing each other.
        Line(mesh, 4, 8.5f, 19, 8.5f);
        Line(mesh, 15.5f, 5, 19, 8.5f);
        Line(mesh, 15.5f, 12, 19, 8.5f);
        Line(mesh, 20, 15.5f, 5, 15.5f);
        Line(mesh, 8.5f, 12, 5, 15.5f);
        Line(mesh, 8.5f, 19, 5, 15.5f);
        break;
      case UiIconKind.Rotate:
        // Most of a circle with an arrow on its end.
        Arc(mesh, 12, 12.5f, 7, -60, 220);
        Line(mesh, 15.5f, 6.4f, 19.6f, 6.4f);
        Line(mesh, 19.6f, 2.3f, 19.6f, 6.4f);
        break;
      case UiIconKind.RemoveStop:
        // A bin with its lid.
        Line(mesh, 4.5f, 7, 19.5f, 7);
        Line(mesh, 9.5f, 7, 9.5f, 4.5f);
        Line(mesh, 9.5f, 4.5f, 14.5f, 4.5f);
        Line(mesh, 14.5f, 4.5f, 14.5f, 7);
        Line(mesh, 6.5f, 7, 7.5f, 19.5f);
        Line(mesh, 7.5f, 19.5f, 16.5f, 19.5f);
        Line(mesh, 16.5f, 19.5f, 17.5f, 7);
        break;
      case UiIconKind.Layers:
        // A diamond sheet lying over the edge of another, as a stack is drawn.
        BeginPath();
        PathPoint(12, 3.5f);
        PathPoint(21, 8.5f);
        PathPoint(12, 13.5f);
        PathPoint(3, 8.5f);
        Stroke(mesh, 2, true);
        BeginPath();
        PathPoint(3, 13.5f);
        PathPoint(12, 18.5f);
        PathPoint(21, 13.5f);
        Stroke(mesh);
        break;
      case UiIconKind.Group:
        // Corners of a frame around two boxes.
        Line(mesh, 3, 3, 7, 3);
        Line(mesh, 3, 3, 3, 7);
        Line(mesh, 17, 3, 21, 3);
        Line(mesh, 21, 3, 21, 7);
        Line(mesh, 3, 17, 3, 21);
        Line(mesh, 3, 21, 7, 21);
        Line(mesh, 21, 17, 21, 21);
        Line(mesh, 17, 21, 21, 21);
        Box(mesh, 7, 7, 13, 13);
        Box(mesh, 11, 11, 17, 17);
        break;
      case UiIconKind.Ungroup:
        Box(mesh, 3, 3, 11, 11);
        Box(mesh, 13, 13, 21, 21);
        break;
      case UiIconKind.Duplicate:
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
        break;
      case UiIconKind.Edit:
        BeginPath();
        PathPoint(4, 16);
        PathPoint(15, 5);
        PathPoint(19, 9);
        PathPoint(8, 20);
        PathPoint(3, 21);
        PathPoint(4, 16);
        Stroke(mesh);
        Line(mesh, 12, 8, 16, 12);
        break;
      case UiIconKind.Export:
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
