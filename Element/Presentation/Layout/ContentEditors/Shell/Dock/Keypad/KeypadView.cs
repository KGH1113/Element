using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Declarative.Controls;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Font = ElementUI.Font;

namespace Element.Presentation.Layout.ContentEditors.Shell.Dock.Keypad;

/// <summary>
/// The keypad: four columns of capsule keys, the operators in the accent,
/// then AC and a wide, tinted Done.
/// </summary>
internal sealed class KeypadView : View<KeypadViewModel>
{
  public const float Key = 68,
    KeyHeight = 44,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    Height = 5 * KeyHeight + 4 * Gap;

  public KeypadView(KeypadViewModel vm)
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
      KeypadKeyViewModel key = VM.Keys[i];
      keys[i] = new KeypadKeyView(key, key.Symbol ? 22 : 20, false);
    }
    return keys;
  }
}
