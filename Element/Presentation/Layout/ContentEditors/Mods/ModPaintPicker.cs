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
/// A tool of the property column (<see cref="ModParts.PaintPicker"/>): like
/// the text editor's Text color, its two columns reach left of the column.
/// </summary>
internal sealed class ModPaintPicker : NativeView
{
  /// <summary>How wide it is, from the column's right edge.</summary>
  public const float Width = ColorPickerColumnsView.TwoColumnWidth;

  private readonly string _title;
  private readonly Binding<Paint> _paint;
  private UiColorPicker _picker;

  public ModPaintPicker(string title, Binding<Paint> paint)
  {
    _title = title;
    _paint = paint;
  }

  /// <summary>The picker widget, for checks.</summary>
  internal UiColorPicker Picker => _picker;

  protected override void Build(RectTransform rect)
  {
    // It sits on the editor's dimmed view, as the text editor's does.
    _picker = new UiColorPicker(rect, UguiTheme.Font, () => UguiTheme.MotionSeconds <= 0);
    _picker.LayOutAsColumn(ModEditorColumn.Width);
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
    // Hiding ends typing in its fields.
    _picker.Hide();
    _picker.Dispose();
    _picker = null;
  }
}
