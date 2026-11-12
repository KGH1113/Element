using System;
using Element.Presentation.Ui.ColorPicker;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Element's color picker in a screen: the color square, the hue and opacity
/// bars, HEX and opacity, and a title with a close button
/// (<see cref="ColorPickerCardView"/>). It writes the color as the player
/// drags or types, and the screen applies it like any control's edit. Made
/// for a popover (<see cref="ColorSwatch"/>).
/// </summary>
internal static class ColorPickerView
{
  /// <param name="title">Shown at the top, such as "Body color".</param>
  /// <param name="color">The color it shows and writes.</param>
  /// <param name="opacity">Whether it edits the alpha too; without it the color is written opaque.</param>
  /// <param name="defaultColor">Without opacity, a Default button puts this color back; null shows none.</param>
  /// <param name="open">Set false by the close button, and when the screen hides; null hides nothing.</param>
  public static View Of(
    string title,
    Binding<Color> color,
    bool opacity,
    Color? defaultColor = null,
    Binding<bool> open = null
  )
  {
    ColorPickerModel model = new();
    if (defaultColor is Color fallback)
      model.SetDefault(fallback);
    model.CloseRequested = () =>
    {
      if (open != null && open.Value)
        open.Value = false;
    };
    model.Show(
      title,
      color.Value,
      opacity,
      picked =>
      {
        color.Value = picked;
        // The binding's own field is followed already; what the write changed
        // besides it, such as a map color becoming a custom one, is seen here.
        Runtime.Probe();
      }
    );
    return ZStack(
      Alignment.TopLeading,
      new ColorPickerCardView(model),
      new ClosesWithScreen(() => model.Close.TryExecute(), model.Closed)
    );
  }

  /// <summary>
  /// Nothing to see: closes the picker when its window or page hides (not
  /// left open behind it), and lets its fields stop reporting once it goes.
  /// </summary>
  private sealed class ClosesWithScreen : NativeView
  {
    private readonly Action _hidden;
    private readonly Action _gone;
    private DeactivationSignal _signal;

    public ClosesWithScreen(Action hidden, Action gone)
    {
      _hidden = hidden;
      _gone = gone;
    }

    protected override void Build(RectTransform rect)
    {
      _signal = rect.gameObject.AddComponent<DeactivationSignal>();
      _signal.Deactivated = _hidden;
    }

    protected override Size PreferredSize(Proposal proposal) => Size.Zero;

    protected override void Teardown()
    {
      if (_signal != null)
        _signal.Deactivated = null;
      _gone();
    }
  }
}
