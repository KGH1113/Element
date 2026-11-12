using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Ui.ColorPicker;
using ElementKit;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Element's full color picker as a view, for a mod's <see cref="Paint"/>:
/// the player's highlight color, a color of their own with opacity, or a
/// gradient, laid out as the overlay editors' column. It writes the paint
/// as the player picks, and the screen takes it like any control's edit.
/// Made for a popover (<see cref="ModParts.PaintPicker"/>).
/// </summary>
internal sealed class ModPaintPicker : NativeView
{
  private const float ColumnWidth = 300;

  private readonly string _title;
  private readonly Binding<Paint> _paint;
  private readonly Binding<bool> _open;
  private UiColorPicker _picker;

  public ModPaintPicker(string title, Binding<Paint> paint, Binding<bool> open)
  {
    _title = title;
    _paint = paint;
    _open = open;
  }

  /// <summary>The picker widget, for checks.</summary>
  internal UiColorPicker Picker => _picker;

  protected override void Build(RectTransform rect)
  {
    _picker = new UiColorPicker(rect, UguiTheme.Font, () => UguiTheme.MotionSeconds <= 0);
    _picker.LayOutAsColumn(ColumnWidth);
    _picker.Hidden = Close;
    _picker.ShowSource(_title, ModPaints.ToSettings(_paint.Value), Changed);
  }

  private void Changed(OverlayColorSettings color)
  {
    _paint.Value = ModPaints.FromSettings(color);
    Runtime.Probe();
    NotifyEdited();
    // A gradient adds its bar: the picker may be taller now.
    SizeChanged();
  }

  private void Close()
  {
    if (_open.Value)
      _open.Value = false;
  }

  protected override Size PreferredSize(Proposal proposal) =>
    _picker == null ? Size.Zero : new Size(_picker.CurrentWidth, _picker.Height);

  protected override void Resized(float width, float height)
  {
    if (_picker != null)
      _picker.Root.anchoredPosition = Vector2.zero;
  }

  protected override void Teardown()
  {
    if (_picker == null)
      return;
    // Hiding ends typing in its fields; the popover is closing already.
    _picker.Hidden = null;
    _picker.Hide();
    _picker.Dispose();
    _picker = null;
  }
}
