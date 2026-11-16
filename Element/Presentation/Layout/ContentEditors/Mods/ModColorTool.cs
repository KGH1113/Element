using Element.Presentation.Ui.ColorPicker;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// Element's color picker as a tool of the column, for a mod's plain
/// <see cref="Color"/>: the color square, hue and opacity, and HEX, in the
/// property column where the text editor's picker shows the color's own controls. It writes the color as the player picks,
/// and the screen takes it like any control's edit.
/// </summary>
internal sealed class ModColorTool : NativeView
{
  private readonly string _title;
  private readonly Binding<Color> _color;
  private readonly bool _opacity;
  private UiColorPicker _picker;

  public ModColorTool(string title, Binding<Color> color, bool opacity)
  {
    _title = title;
    _color = color;
    _opacity = opacity;
  }

  /// <summary>The picker widget, for checks.</summary>
  internal UiColorPicker Picker => _picker;

  protected override void Build(RectTransform rect)
  {
    _picker = new UiColorPicker(rect, UguiTheme.Font, () => UguiTheme.MotionSeconds <= 0);
    // The color square's width: it ends with the column, as the text editor's does.
    _picker.LayOutAsColumn(ColorPickerLayout.Column);
    _picker.Show(_title, _color.Value, _opacity, Changed);
  }

  private void Changed(Color color)
  {
    _color.Value = _opacity ? color : new Color(color.r, color.g, color.b, 1);
    Runtime.Probe();
    NotifyEdited();
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
    _picker.Hide();
    _picker.Dispose();
    _picker = null;
  }
}
