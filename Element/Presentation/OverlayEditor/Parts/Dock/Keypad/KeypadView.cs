using ElementUI;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// The keypad: four columns of capsule keys, the operators in the accent.
/// Its sizes are the right column's and every input method's: they all take
/// its place (<see cref="InputMethodView"/>), the whole of it. Nothing is
/// under it: a value is taken out with the × on its pill, and the field is
/// done with Enter, Esc or a click elsewhere.
/// </summary>
internal sealed class KeypadView : View<KeypadViewModel>
{
  public const float Key = 80,
    Gap = 8,
    Width = 4 * Key + 3 * Gap,
    // The place every input method has.
    Height = 302,
    MethodHeight = Height,
    // A key of the pad: four rows fill the place.
    PadKeyHeight = (MethodHeight - 3 * Gap) / 4;

  private readonly float _keyHeight;

  /// <param name="keyHeight">How tall a key is: lower where a folded condition has the top of the place.</param>
  public KeypadView(KeypadViewModel vm, float keyHeight = PadKeyHeight)
    : base(vm) => _keyHeight = keyHeight;

  /// <summary>How tall a key is when the pad has <paramref name="height"/> for its four rows.</summary>
  public static float KeyHeightIn(float height) => (height - 3 * Gap) / 4;

  public override View Body => Grid(4, Gap, Keys()).Frame(width: Width, height: 4 * _keyHeight + 3 * Gap);

  private View[] Keys()
  {
    View[] keys = new View[VM.Keys.Count];
    for (int i = 0; i < keys.Length; i++)
    {
      KeypadKeyViewModel key = VM.Keys[i];
      keys[i] = new KeypadKeyView(key, key.Symbol ? 24 : 22, _keyHeight);
    }
    return keys;
  }
}
