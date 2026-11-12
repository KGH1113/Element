using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// The keypad: four columns of see-through capsule keys, the operators in
/// the accent, then AC and a wide, tinted Done.
/// </summary>
internal sealed class OverlayKeypadView : View<OverlayKeypadModel>
{
  public const float Key = 68,
    KeyHeight = 46,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    Height = 5 * KeyHeight + 4 * Gap;

  public OverlayKeypadView(OverlayKeypadModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: Gap,
        Grid(4, Gap, Keys()),
        HStack(
          spacing: Gap,
          new KeypadKeyView(VM.Clear, 17, false).Frame(width: Key),
          // Done is the one filled key: the way out of the keypad.
          new KeypadKeyView(VM.Done, 17, true).Frame(maxWidth: float.PositiveInfinity)
        )
      )
      .Frame(width: Width, height: Height);

  private View[] Keys()
  {
    View[] keys = new View[VM.Keys.Count];
    for (int i = 0; i < keys.Length; i++)
    {
      KeypadKeyModel key = VM.Keys[i];
      keys[i] = new KeypadKeyView(key, key.Symbol ? 22 : 20, false);
    }
    return keys;
  }
}

/// <summary>One key: a capsule that fills its cell, faint and with its label dimmed while it can't be pressed.</summary>
internal sealed class KeypadKeyView : View<KeypadKeyModel>
{
  private readonly float _size;
  private readonly UiColor _fill,
    _offFill,
    _ink,
    _offInk;

  /// <param name="size">The label's size.</param>
  /// <param name="tinted">Filled with the accent, as Done.</param>
  public KeypadKeyView(KeypadKeyModel vm, float size, bool tinted)
    : base(vm)
  {
    _size = size;
    Color accent = UiPalette.SoftAccent;
    _fill = tinted ? new Color(accent.r, accent.g, accent.b, .32f) : new Color(1, 1, 1, .07f);
    _offFill = tinted ? new Color(accent.r, accent.g, accent.b, .14f) : new Color(1, 1, 1, .03f);
    Color ink = vm.Symbol ? accent : UiPalette.Foreground;
    _ink = ink;
    _offInk = new Color(ink.r, ink.g, ink.b, .3f);
  }

  public override View Body =>
    Button(Label().Frame(maxWidth: float.PositiveInfinity, maxHeight: float.PositiveInfinity), () => VM.Press, ButtonStyle.Plain)
      .Background(() => VM.Enabled.Value ? _fill : _offFill, OverlayKeypadView.KeyHeight / 2)
      .Named(VM.Name)
      .Frame(height: OverlayKeypadView.KeyHeight);

  private View Label() =>
    VM.Character == '\b'
      ? Image(ElementIcons.Of(UiIconKind.Backspace), 24).Foreground(() => VM.Enabled.Value ? _ink : _offInk)
      : UI.Text(VM.Label).Font(Font.Body.Sized(_size)).Foreground(() => VM.Enabled.Value ? _ink : _offInk).LineLimit(1);
}
