using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

/// <summary>
/// The hue bar (the spectrum, its handle holding the pure hue) or the opacity
/// bar (a checkerboard into the color). The handle reaches past both ends of
/// the bar by half its size, so the slider takes that much more room than it
/// is laid out in.
/// </summary>
internal sealed class ColorSliderView : NativeView
{
  public const float Height = 32;
  private const float Track = 12,
    Handle = 20,
    Overhang = Handle / 2;

  private readonly ColorPickerViewModel _vm;
  private readonly bool _hue;
  private UiCapsuleGraphic _ring,
    _center;
  private UiAlphaGraphic _checker;

  /// <param name="hue">The hue bar; otherwise the opacity bar.</param>
  public ColorSliderView(ColorPickerViewModel vm, bool hue)
  {
    _vm = vm;
    _hue = hue;
  }

  /// <summary>The slider, for checks.</summary>
  internal Slider Slider { get; private set; }

  protected override void Build(RectTransform rect)
  {
    RectTransform root = RectPeer.Child(rect, _hue ? "Hue" : "Opacity");
    RectPeer.Stretch(root, -Overhang, 0, -Overhang, 0);
    Image hit = root.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    UiSlider slider = root.gameObject.AddComponent<UiSlider>();
    slider.Step = _hue ? 1f / 360 : .01f;
    slider.navigation = new Navigation { mode = Navigation.Mode.None };
    UiCursor.Attach(root.gameObject, UiCursorKind.Horizontal);

    RectTransform track = RectPeer.Child(root, _hue ? "Hue spectrum" : "Opacity track");
    float inset = (Height - Track) / 2;
    RectPeer.Stretch(track, Overhang, inset, Overhang, inset);
    UiCapsuleGraphic shape = track.gameObject.AddComponent<UiCapsuleGraphic>();
    shape.color = Color.white;
    shape.raycastTarget = false;
    track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform fill = RectPeer.Child(track, _hue ? "Spectrum" : "Alpha checkerboard");
    if (_hue)
    {
      UiGradientGraphic spectrum = fill.gameObject.AddComponent<UiGradientGraphic>();
      spectrum.HueStrip = true;
      spectrum.raycastTarget = false;
    }
    else
    {
      _checker = fill.gameObject.AddComponent<UiAlphaGraphic>();
      _checker.raycastTarget = false;
    }

    RectTransform area = RectPeer.Child(root, "Handle area");
    inset = (Height - Handle) / 2;
    // The handle stops at the track's ends, so it never sticks out of the column.
    RectPeer.Stretch(area, Overhang + Handle / 2, inset, Overhang + Handle / 2, inset);
    RectTransform handle = RectPeer.Child(area, "Handle");
    handle.pivot = new Vector2(.5f, .5f);
    handle.anchorMin = handle.anchorMax = new Vector2(0, .5f);
    handle.anchoredPosition = Vector2.zero;
    handle.sizeDelta = new Vector2(Handle, 0);
    // Every handle is made alike: a hairline, a ring that follows the color, the color it holds.
    UiCapsuleGraphic hairline = handle.gameObject.AddComponent<UiCapsuleGraphic>();
    hairline.color = UiPalette.HandleHairline;
    hairline.raycastTarget = false;
    RectTransform ring = RectPeer.Child(handle, _hue ? "Hue marker ring" : "Opacity marker ring");
    RectPeer.Stretch(ring, 1, 1, 1, 1);
    _ring = ring.gameObject.AddComponent<UiCapsuleGraphic>();
    _ring.raycastTarget = false;
    RectTransform center = RectPeer.Child(handle, _hue ? "Hue marker center" : "Opacity marker center");
    RectPeer.Stretch(center, 5, 5, 5, 5);
    _center = center.gameObject.AddComponent<UiCapsuleGraphic>();
    _center.raycastTarget = false;
    slider.handleRect = handle;
    slider.targetGraphic = _ring;
    slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
    slider.minValue = 0;
    slider.maxValue = 1;
    slider.onValueChanged.AddListener(Moved);
    Slider = slider;
    if (_hue)
      Watch(() => _vm.Hsv.Value, ShowHue);
    else
    {
      Watch(
        () => _vm.Alpha.Value,
        alpha =>
        {
          Slider.SetValueWithoutNotify(alpha);
          ShowOpacity();
        }
      );
      Watch(
        () => _vm.Swatch.Value,
        color =>
        {
          _checker.SetColor(color);
          ShowOpacity();
        }
      );
    }
  }

  private void ShowHue(Vector3 hsv)
  {
    Slider.SetValueWithoutNotify(hsv.x);
    Color pure = Color.HSVToRGB(hsv.x, 1, 1);
    _center.color = pure;
    _ring.color = UiPalette.HandleEdge(pure);
  }

  private void ShowOpacity()
  {
    Color color = _vm.Swatch.Value;
    _center.color = color;
    color.a = _vm.Alpha.Value;
    _ring.color = UiPalette.HandleEdge(color);
  }

  private void Moved(float value)
  {
    // A bar that didn't move has nothing to report; Unity's editor reports
    // every slider again when its canvas is rebuilt.
    float shown = _hue ? _vm.Hsv.Value.x : _vm.Alpha.Value;
    if (Mathf.Abs(value - shown) < .0001f)
      return;
    if (_hue)
      _vm.PickHue(value);
    else
      _vm.PickAlpha(value);
    NotifyEdited();
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 256, Height);
}
