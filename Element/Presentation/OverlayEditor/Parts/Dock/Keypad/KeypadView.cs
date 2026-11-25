using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// The keypad: a short row of the six comparisons, four columns of capsule
/// keys with the operators in the accent, then AC, "then" (or "otherwise")
/// and a tinted Apply.
/// </summary>
internal sealed class KeypadView : View<KeypadViewModel>
{
  public const float Key = 68,
    KeyHeight = 44,
    CompareHeight = 30,
    CompareGap = 6,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    Height = CompareHeight + Gap + 5 * KeyHeight + 4 * Gap;

  public KeypadView(KeypadViewModel vm)
    : base(vm) { }

  public override View Body =>
    VStack(
        spacing: Gap,
        Grid(VM.Compares.Count, CompareGap, Compares()),
        Grid(4, Gap, Keys()),
        HStack(
          spacing: Gap,
          new KeypadKeyView(VM.Clear, 17, false).Frame(width: Key),
          new KeypadKeyView(VM.Next, 17, false).Frame(width: Key),
          // Apply is the one filled key: the way out of the keypad.
          new KeypadKeyView(VM.Done, 17, true).Frame(maxWidth: float.PositiveInfinity)
        )
      )
      .Frame(width: Width, height: Height);

  private View[] Compares()
  {
    View[] keys = new View[VM.Compares.Count];
    for (int i = 0; i < keys.Length; i++)
      keys[i] = new KeypadKeyView(VM.Compares[i], 17, false, CompareHeight);
    return keys;
  }

  private View[] Keys()
  {
    View[] keys = new View[VM.Keys.Count];
    for (int i = 0; i < keys.Length; i++)
    {
      KeypadKeyViewModel key = VM.Keys[i];
      keys[i] = new KeypadKeyView(key, key.Symbol ? 22 : 20, false);
    }
    return keys;
  }
}
