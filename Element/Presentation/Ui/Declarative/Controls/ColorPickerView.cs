using System;
using Element.Presentation.Ui.ColorPicker;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Element's color picker as a view: the color square, the hue and opacity
/// bars, HEX and opacity, and a title with a close button. It writes the
/// color as the player drags or types, and the screen applies it like any
/// control's edit. Made for a popover (<see cref="ColorSwatch"/>); on a
/// screen too small for it, it shrinks to fit.
/// </summary>
internal sealed class ColorPickerView : NativeView
{
  private readonly string _title;
  private readonly Binding<Color> _color;
  private readonly bool _opacity;
  private readonly Color? _default;
  private readonly Binding<bool> _open;
  private UiColorPicker _picker;
  private DeactivationSignal _signal;

  /// <param name="title">Shown at the top, such as "Body color".</param>
  /// <param name="color">The color it shows and writes.</param>
  /// <param name="opacity">Whether it edits the alpha too; without it the color is written opaque.</param>
  /// <param name="defaultColor">Without opacity, a Default button puts this color back; null shows none.</param>
  /// <param name="open">Set false by the close button; null hides nothing.</param>
  public ColorPickerView(
    string title,
    Binding<Color> color,
    bool opacity,
    Color? defaultColor = null,
    Binding<bool> open = null
  )
  {
    _title = title;
    _color = color;
    _opacity = opacity;
    _default = defaultColor;
    _open = open;
  }

  /// <summary>The picker widget, for checks.</summary>
  internal UiColorPicker Picker => _picker;

  protected override void Build(RectTransform rect)
  {
    _picker = new UiColorPicker(rect, UguiTheme.Font, () => UguiTheme.MotionSeconds <= 0);
    if (_default is Color fallback)
      _picker.SetDefault(fallback);
    _picker.Hidden = Close;
    _picker.Show(_title, _color.Value, _opacity, Changed);
    // Hidden with its page or its window: closed, not left open behind it.
    _signal = rect.gameObject.AddComponent<DeactivationSignal>();
    _signal.Deactivated = Close;
    BringLayerToFront();
  }

  // ElementUI makes the popover layer before the screen's own parts, so the
  // screen would draw, and take clicks, over an open popover (ElementUI
  // 11316be). The open picker brings its layer to the front: the popover's
  // box holds this view, and the layer holds the box.
  private void BringLayerToFront()
  {
    Transform layer = Rect.parent != null ? Rect.parent.parent : null;
    if (layer != null && layer.parent != null && layer.GetSiblingIndex() != layer.parent.childCount - 1)
      layer.SetAsLastSibling();
  }

  private void Changed(Color color)
  {
    _color.Value = color;
    // The binding's own field is followed already; what the write changed
    // besides it, such as a map color becoming a custom one, is seen here.
    Runtime.Probe();
    NotifyEdited();
  }

  private void Close()
  {
    if (_open != null && _open.Value)
      _open.Value = false;
  }

  protected override Size PreferredSize(Proposal proposal)
  {
    float width = UiColorPicker.Width,
      height = _picker?.Height ?? 0;
    // Narrower than the picker: the same shape, smaller.
    if (!float.IsNaN(proposal.Width) && !float.IsInfinity(proposal.Width) && proposal.Width < width)
      return new Size(proposal.Width, height * proposal.Width / width);
    return new Size(width, height);
  }

  protected override void Resized(float width, float height)
  {
    if (_picker == null || _picker.Height <= 0)
      return;
    float scale = Mathf.Min(1, width / UiColorPicker.Width, height / _picker.Height);
    _picker.Root.localScale = new Vector3(scale, scale, 1);
    _picker.Root.anchoredPosition = Vector2.zero;
    // Parts the screen made since it opened went in front of it.
    BringLayerToFront();
  }

  protected override void Teardown()
  {
    if (_picker == null)
      return;
    _signal.Deactivated = null;
    // Hiding ends typing in its fields; the popover is closing already.
    _picker.Hidden = null;
    _picker.Hide();
    _picker.Dispose();
    _picker = null;
  }
}
