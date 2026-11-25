using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// The keypad: four columns of capsule keys, the operators in the accent.
/// Its sizes are every input method's: they all take its place
/// (<see cref="InputMethodView"/>), over the row of AC, back and Apply.
/// </summary>
internal sealed class KeypadView : View<KeypadViewModel>
{
  public const float Key = 68,
    KeyHeight = 44,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    // What an input method has above the shared row.
    MethodHeight = 4 * KeyHeight + 3 * Gap,
    Height = MethodHeight + Gap + KeyHeight;

  public KeypadView(KeypadViewModel vm)
    : base(vm) { }

  public override View Body => Grid(4, Gap, Keys()).Frame(width: Width, height: MethodHeight);

  /// <summary>The row under every input method: AC, back out of a slot, and a wide, tinted Apply.</summary>
  public static View SharedRow(KeypadViewModel vm) =>
    HStack(
        spacing: Gap,
        new KeypadKeyView(vm.Clear, 17, false).Frame(width: Key),
        new KeypadKeyView(vm.Back, 15, false).Frame(width: Key),
        // Apply is the one filled key: the way out of the dock.
        new KeypadKeyView(vm.Done, 17, true).Frame(maxWidth: float.PositiveInfinity)
      )
      .Frame(width: Width, height: KeyHeight);

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
