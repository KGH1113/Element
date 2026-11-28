using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>One key: a capsule that fills its cell, faint and with its label dimmed while it can't be pressed.</summary>
internal sealed class KeypadKeyView : View<KeypadKeyViewModel>
{
  private readonly float _size;
  private readonly float _height;
  private readonly UiColor _fill,
    _offFill,
    _ink,
    _offInk;

  /// <param name="size">The label's size.</param>
  /// <param name="height">How tall it is: a key of the pad, or of a shorter row.</param>
  public KeypadKeyView(KeypadKeyViewModel vm, float size, float height = KeypadView.PadKeyHeight)
    : base(vm)
  {
    _size = size;
    _height = height;
    Color accent = UiPalette.SoftAccent;
    // Opaque, so the overlay behind never shows through a key.
    _fill = UiPalette.EditorControl;
    _offFill = UiPalette.EditorControlOff;
    Color ink = vm.Symbol ? accent : UiPalette.Foreground;
    _ink = ink;
    _offInk = new Color(ink.r, ink.g, ink.b, .3f);
  }

  public override View Body =>
    Button(
        Label().Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity),
        () => VM.Press,
        ButtonStyle.Plain
      )
      .Background(() => VM.Enabled.Value ? _fill : _offFill, _height / 2)
      .Named(VM.Name)
      .Frame(height: _height);

  private View Label() =>
    VM.Character == '\b'
      ? Image(ElementIcons.Of(UiIconKind.Backspace), 24).Foreground(() => VM.Enabled.Value ? _ink : _offInk)
    // An operator's icon is as large as a comparison's; other icons a little smaller.
    : VM.Icon is { } icon
      ? Image(ElementIcons.Of(icon), VM.Symbol ? 24 : 20).Foreground(() => VM.Enabled.Value ? _ink : _offInk)
    : VM.Saying != null
      ? UI.Text(() => VM.Saying.Value)
        .Font(Font.Body.Sized(_size))
        .Foreground(() => VM.Enabled.Value ? _ink : _offInk)
        .LineLimit(1)
    : UI.Text(VM.Label).Font(Font.Body.Sized(_size)).Foreground(() => VM.Enabled.Value ? _ink : _offInk).LineLimit(1);
}
