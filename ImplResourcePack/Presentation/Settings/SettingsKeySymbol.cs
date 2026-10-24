using UnityEngine;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

// macOS modifier glyphs drawn as vectors: the overlay font has no ⌘/⌥ atlas glyphs.
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsKeySymbol : SettingsVectorGraphic
{
  private KeyCode _key;

  public void SetKey(KeyCode key)
  {
    if (_key == key)
      return;
    _key = key;
    SetVerticesDirty();
  }

  public static string Symbol(KeyCode key) =>
    key switch
    {
      KeyCode.LeftCommand or KeyCode.RightCommand => "⌘",
      KeyCode.LeftAlt or KeyCode.RightAlt => "⌥",
      KeyCode.LeftControl or KeyCode.RightControl => "⌃",
      KeyCode.LeftShift or KeyCode.RightShift => "⇧",
      _ => "",
    };

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    switch (_key)
    {
      case KeyCode.LeftCommand:
      case KeyCode.RightCommand:
        Arc(mesh, 6, 6, 4, 0, -270, 1.8f);
        Line(mesh, 6, 10, 18, 10);
        Arc(mesh, 18, 6, 4, 90, -180, 1.8f);
        Line(mesh, 14, 6, 14, 18);
        Arc(mesh, 18, 18, 4, 180, -90, 1.8f);
        Line(mesh, 18, 14, 6, 14);
        Arc(mesh, 6, 18, 4, 270, 0, 1.8f);
        Line(mesh, 10, 18, 10, 6);
        break;
      case KeyCode.LeftAlt:
      case KeyCode.RightAlt:
        BeginPath();
        PathPoint(3, 6);
        PathPoint(8, 6);
        PathPoint(16, 18);
        PathPoint(22, 18);
        Stroke(mesh, 1.8f);
        Line(mesh, 14, 6, 22, 6);
        break;
      case KeyCode.LeftControl:
      case KeyCode.RightControl:
        BeginPath();
        PathPoint(5, 15);
        PathPoint(12, 8);
        PathPoint(19, 15);
        Stroke(mesh, 1.8f);
        break;
      case KeyCode.LeftShift:
      case KeyCode.RightShift:
        BeginPath();
        PathPoint(12, 3);
        PathPoint(3, 12);
        PathPoint(8, 12);
        PathPoint(8, 21);
        PathPoint(16, 21);
        PathPoint(16, 12);
        PathPoint(21, 12);
        Stroke(mesh, 1.8f, true);
        break;
    }
  }
}
