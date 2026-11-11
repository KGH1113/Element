using Element.Presentation.Settings;
using UnityEngine;

namespace Element.Presentation.Ui.Popovers;

/// <summary>The settings color picker as a popover opened from a control.</summary>
internal sealed class PickerPopover : IPopover
{
  public PickerPopover(SettingsColorPicker picker) => Picker = picker;

  public SettingsColorPicker Picker { get; }

  public RectTransform Root => Picker.Root;

  public bool Visible => Picker.Visible;

  public bool IsTyping => Picker.Visible && Picker.IsTyping;

  public RectTransform Anchor { get; set; }

  public bool Contains(Vector2 screen) => PopoverPlacement.Contains(Picker.Root, screen);

  public void Hide() => Picker.Hide();
}
