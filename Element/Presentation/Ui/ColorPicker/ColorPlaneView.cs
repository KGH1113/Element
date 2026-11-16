using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The color square: saturation across, brightness up, in the picked hue,
/// with a ring at the color. Dragging on it picks there; the crosshair says so.
/// </summary>
internal sealed class ColorPlaneView : NativeView
{
  public const float Height = 156;

  private readonly ColorPickerModel _vm;
  private UiColorPlane _plane;
  private UiCapsuleGraphic _picked;

  public ColorPlaneView(ColorPickerModel vm) => _vm = vm;

  protected override void Build(RectTransform rect)
  {
    RectTransform area = RectPeer.Child(rect, "Saturation and brightness");
    // A drawn stencil keeps the corners round without stretching a sprite over the square.
    UiColorMask mask = area.gameObject.AddComponent<UiColorMask>();
    mask.color = Color.white;
    mask.raycastTarget = true;
    area.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    _plane = area.gameObject.AddComponent<UiColorPlane>();
    _plane.navigation = new Navigation { mode = Navigation.Mode.None };
    _plane.transition = Selectable.Transition.None;
    _plane.targetGraphic = mask;
    UiCursor.Attach(area.gameObject, UiCursorKind.Crosshair);
    RectTransform gradient = RectPeer.Child(area, "HSV gradient");
    _plane.Gradient = gradient.gameObject.AddComponent<UiGradientGraphic>();
    _plane.Gradient.raycastTarget = false;
    RectTransform marker = Capsule(area, "Color position", 18, Color.white).rectTransform;
    marker.pivot = new Vector2(.5f, .5f);
    _plane.Marker = marker;
    _picked = Capsule(marker, "Selected color", 12, Color.white);
    _picked.rectTransform.anchoredPosition = new Vector2(3, -3);
    _plane.Changed = (saturation, value) =>
    {
      _vm.PickSaturationValue(saturation, value);
      NotifyEdited();
    };
    Watch(() => _vm.Hsv.Value, hsv => _plane.Set(hsv.x, hsv.y, hsv.z));
    Watch(() => _vm.Swatch.Value, color => _picked.color = color);
  }

  private static UiCapsuleGraphic Capsule(Transform parent, string name, float size, Color color)
  {
    RectTransform rect = RectPeer.Child(parent, name);
    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
    rect.sizeDelta = new Vector2(size, size);
    UiCapsuleGraphic capsule = rect.gameObject.AddComponent<UiCapsuleGraphic>();
    capsule.color = color;
    capsule.raycastTarget = false;
    return capsule;
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 256, Height);
}
