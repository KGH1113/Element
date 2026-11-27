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
/// bar (a checkerboard into the color), lying or upright (beside a color
/// square in a narrow place, so the picker doesn't run wide). The handle
/// reaches past both ends of the bar by half its size, so the slider takes
/// that much more room than it is laid out in.
/// </summary>
internal sealed class ColorSliderView : NativeView
{
  public const float Height = 32;
  private const float Track = 12,
    Handle = 20,
    Overhang = Handle / 2;

  private readonly ColorPickerViewModel _vm;
  private readonly bool _hue;
  private readonly bool _upright;
  private UiCapsuleGraphic _ring,
    _center;
  private UiAlphaGraphic _checker;

  /// <param name="hue">The hue bar; otherwise the opacity bar.</param>
  /// <param name="upright">Standing, as tall as it is laid out and <see cref="Height"/> wide; the hue runs from red at the top.</param>
  public ColorSliderView(ColorPickerViewModel vm, bool hue, bool upright = false)
  {
    _vm = vm;
    _hue = hue;
    _upright = upright;
  }

  /// <summary>The slider, for checks.</summary>
  internal Slider Slider { get; private set; }

  protected override void Build(RectTransform rect)
  {
    RectTransform root = RectPeer.Child(rect, _hue ? "Hue" : "Opacity");
    // Along the bar: across it for a lying one, up and down for an upright one.
    Along(root, -Overhang, 0);
    Image hit = root.gameObject.AddComponent<Image>();
    hit.color = Color.clear;
    UiSlider slider = root.gameObject.AddComponent<UiSlider>();
    slider.Step = _hue ? 1f / 360 : .01f;
    slider.navigation = new Navigation { mode = Navigation.Mode.None };
    UiCursor.Attach(root.gameObject, _upright ? UiCursorKind.Vertical : UiCursorKind.Horizontal);

    RectTransform track = RectPeer.Child(root, _hue ? "Hue spectrum" : "Opacity track");
    float inset = (Height - Track) / 2;
    Along(track, Overhang, inset);
    UiCapsuleGraphic shape = track.gameObject.AddComponent<UiCapsuleGraphic>();
    shape.color = Color.white;
    shape.raycastTarget = false;
    track.gameObject.AddComponent<Mask>().showMaskGraphic = true;
    RectTransform fill = RectPeer.Child(track, _hue ? "Spectrum" : "Alpha checkerboard");
    if (_hue)
    {
      UiGradientGraphic spectrum = fill.gameObject.AddComponent<UiGradientGraphic>();
      spectrum.HueStrip = true;
      spectrum.Upright = _upright;
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
    Along(area, Overhang + Handle / 2, inset);
    RectTransform handle = RectPeer.Child(area, "Handle");
    handle.pivot = new Vector2(.5f, .5f);
    handle.anchorMin = handle.anchorMax = _upright ? new Vector2(.5f, 1) : new Vector2(0, .5f);
    handle.anchoredPosition = Vector2.zero;
    // The slider sets where along the bar it is; across, it fills the area.
    handle.sizeDelta = _upright ? new Vector2(0, Handle) : new Vector2(Handle, 0);
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
    slider.direction = _upright
      ? UnityEngine.UI.Slider.Direction.TopToBottom
      : UnityEngine.UI.Slider.Direction.LeftToRight;
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
    _upright
      ? new(Height, proposal.HasHeight && !float.IsInfinity(proposal.Height) ? proposal.Height : 256)
      : new(proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width : 256, Height);

  // Insets <paramref name="along"/> at the bar's two ends and <paramref name="across"/> at its two sides.
  private void Along(RectTransform rect, float along, float across)
  {
    if (_upright)
      RectPeer.Stretch(rect, across, along, across, along);
    else
      RectPeer.Stretch(rect, along, across, along, across);
  }
}
