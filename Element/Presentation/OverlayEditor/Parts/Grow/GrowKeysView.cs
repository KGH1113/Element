using Element.Presentation.OverlayEditor.Parts.Dock.Keypad;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Grow;

/// <summary>
/// A point's input method, in the keypad's place: the nine points as keys,
/// three by three, and Auto under them.
/// </summary>
internal sealed class GrowKeysView : View<GrowPickerViewModel>
{
  private const float Gap = 8,
    AutoHeight = 40;

  private readonly UiColor _rest = UiPalette.EditorControl;
  private readonly UiColor _lit = Color.Lerp(UiPalette.EditorControl, UiPalette.SoftAccent, .34f);
  private readonly UiColor _edge = UiPalette.SoftAccent;
  private readonly UiColor _noEdge = FieldLook.Clear;

  public GrowKeysView(GrowPickerViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: Gap,
        HAlignment.Leading,
        // Square, as the box they are the points of.
        Grid(3, Gap, Keys()).Frame(width: GrowKeyView.Size * 3 + Gap * 2),
        Button(
            UI.Text(L("자동", "Auto"))
              .Font(Font.Body.Sized(15))
              .LineLimit(1)
              .TextAlignment(TextAlign.Center)
              .Frame(width: GrowKeyView.Size * 3 + Gap * 2, height: AutoHeight),
            () => VM.Auto,
            ButtonStyle.Plain
          )
          .ButtonStyle(new ButtonSurfaceStyle(() => VM.IsAuto.Value ? _lit : _rest, 10))
          .Overlay(
            RoundedRectangle(10).Stroke(() => VM.IsAuto.Value ? _edge : _noEdge, FieldLook.Edge).AllowsHitTesting(false)
          )
          .Named("Grow automatically")
      )
      .Frame(width: KeypadView.Width, height: KeypadView.MethodHeight, alignment: Alignment.TopLeading);

  private View[] Keys()
  {
    View[] keys = new View[VM.Points.Count];
    for (int i = 0; i < keys.Length; i++)
      keys[i] = new GrowKeyView(VM.Points[i], VM);
    return keys;
  }
}
