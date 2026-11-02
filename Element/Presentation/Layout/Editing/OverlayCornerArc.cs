using System;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Layout;

/// <summary>
/// A 120° arc just outside a shape's corner, facing away from the shape. It
/// is the corner's rounding control: hovering lights it, clicking edits it.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class OverlayCornerArc : SettingsVectorGraphic
{
  public const float Size = 44;
  private const float Radius = 14,
    Sweep = 120;

  private float _angle;

  protected override float DesignSize => Size;

  /// <summary>The direction it faces, in degrees clockwise from right (screen y down).</summary>
  public float Angle
  {
    get => _angle;
    set
    {
      if (Mathf.Approximately(_angle, value))
        return;
      _angle = value;
      SetVerticesDirty();
    }
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Arc(mesh, Size * .5f, Size * .5f, Radius, _angle - Sweep * .5f, _angle + Sweep * .5f, 2.6f);
  }

  /// <summary>An arc centered on its corner that calls <paramref name="click"/>.</summary>
  public static Button Create(SettingsUiFactory ui, Transform parent, string name, Action click, bool reduceMotion)
  {
    RectTransform rect = ui.Rect(name, parent, 0, 0, Size, Size);
    OverlayCornerArc arc = rect.gameObject.AddComponent<OverlayCornerArc>();
    arc.color = Color.white;
    arc.raycastTarget = true;
    SettingsButton button = rect.gameObject.AddComponent<SettingsButton>();
    button.navigation = new Navigation { mode = Navigation.Mode.None };
    button.targetGraphic = arc;
    Paint(button, false, reduceMotion);
    SettingsCursor.Attach(button.gameObject, SettingsCursorKind.Pointer);
    button.onClick.AddListener(() => click());
    return button;
  }

  /// <summary>Muted at rest, the accent while hovered or edited.</summary>
  public static void Paint(Button button, bool selected, bool reduceMotion)
  {
    Color accent = SettingsPalette.SoftAccent;
    Color rest = selected ? accent : new Color(1, 1, 1, .5f);
    ((SettingsButton)button).SetPalette(
      new ColorBlock
      {
        normalColor = rest,
        selectedColor = rest,
        highlightedColor = accent,
        pressedColor = Color.Lerp(accent, Color.white, .3f),
        disabledColor = new Color(1, 1, 1, .2f),
        colorMultiplier = 1,
        fadeDuration = reduceMotion ? 0 : .08f,
      }
    );
  }
}
