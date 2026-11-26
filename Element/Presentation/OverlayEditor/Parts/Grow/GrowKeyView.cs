using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// One of the nine points as a key of the input method: an arrow toward its
/// corner or edge (a dot for the middle), lit with the highlight color's
/// outline while it is the point that stays put.
/// </summary>
internal sealed class GrowKeyView : View<GrowPointViewModel>
{
  public const float Size = 60;

  private readonly GrowPickerViewModel _picker;
  private readonly float _angle;
  private readonly bool _middle;
  private readonly UiColor _rest = UiPalette.EditorControl;
  private readonly UiColor _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .34f);
  private readonly UiColor _edge = UiPalette.SoftAccent;
  private readonly UiColor _noEdge = FieldLook.Clear;

  /// <param name="picker">Whether Auto is the choice: then no point is lit.</param>
  public GrowKeyView(GrowPointViewModel vm, GrowPickerViewModel picker)
    : base(vm)
  {
    _picker = picker;
    int index = (int)vm.Growth;
    int dx = index % 3 - 1,
      dy = index / 3 - 1;
    _middle = dx == 0 && dy == 0;
    _angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
  }

  public override View Body =>
    Button(
        (
          _middle
            ? Circle().Fill(Colors.Foreground).Frame(width: 5, height: 5)
            : new GrowArrowView().RotationEffect(() => _angle)
        ).Frame(width: Size, height: Size),
        () => VM.Pick,
        ButtonStyle.Plain
      )
      .ButtonStyle(
        new ButtonSurfaceStyle(() => VM.Look.Value == GrowLook.Chosen && !_picker.IsAuto.Value ? _lit : _rest, 10)
      )
      .Overlay(
        RoundedRectangle(10)
          .Stroke(() => VM.Look.Value == GrowLook.Chosen && !_picker.IsAuto.Value ? _edge : _noEdge, FieldLook.Edge)
          .AllowsHitTesting(false)
      )
      .Named(VM.Name)
      .Frame(width: Size, height: Size);
}
