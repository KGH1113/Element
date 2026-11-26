using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// The keypad: four columns of capsule keys, the operators in the accent.
/// Its sizes are the right column's and every input method's: they all take
/// its place (<see cref="InputMethodView"/>), over the row of Remove and Apply.
/// </summary>
internal sealed class KeypadView : View<KeypadViewModel>
{
  public const float Key = 80,
    // A key of the pad; the row under every input method is a little lower.
    PadKeyHeight = 56,
    KeyHeight = 46,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    // What an input method has above the shared row.
    MethodHeight = 4 * PadKeyHeight + 3 * Gap,
    Height = MethodHeight + Gap + KeyHeight;

  public KeypadView(KeypadViewModel vm)
    : base(vm) { }

  public override View Body => Grid(4, Gap, Keys()).Frame(width: Width, height: MethodHeight);

  /// <summary>The row under every input method: Remove (지우기) the picked value, and a wide Apply.</summary>
  public static View SharedRow(KeypadViewModel vm) =>
    HStack(
        spacing: Gap,
        new KeypadKeyView(vm.Remove, 17, false).Frame(width: 132),
        // Apply is the one lighter key: the way out of the dock.
        new KeypadKeyView(vm.Done, 17, true).Frame(maxWidth: float.PositiveInfinity)
      )
      .Frame(width: Width, height: KeyHeight);

  private View[] Keys()
  {
    View[] keys = new View[VM.Keys.Count];
    for (int i = 0; i < keys.Length; i++)
    {
      KeypadKeyViewModel key = VM.Keys[i];
      keys[i] = new KeypadKeyView(key, key.Symbol ? 24 : 22, false, PadKeyHeight);
    }
    return keys;
  }
}
