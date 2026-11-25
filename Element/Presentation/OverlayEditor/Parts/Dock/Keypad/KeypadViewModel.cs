using System.Collections.Generic;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Formula;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// Large keys for a number: 0–9, a point, + − × ÷ and backspace, then the
/// row every input method shares: AC to empty what is open, back out of a
/// slot, and Apply. A key that would do nothing can't be pressed and reads
/// dimmed; each key follows the variable by itself. A pressed key goes up as
/// the dock's <see cref="IInputDockActions"/>. <see cref="KeypadView"/>
/// draws it.
/// </summary>
internal sealed class KeypadViewModel : ViewModel
{
  // Display symbols and the characters they type, row by row; backspace is '\b'.
  private static readonly (string Label, char Key)[] Layout =
  {
    ("7", '7'),
    ("8", '8'),
    ("9", '9'),
    ("÷", '/'),
    ("4", '4'),
    ("5", '5'),
    ("6", '6'),
    ("×", '*'),
    ("1", '1'),
    ("2", '2'),
    ("3", '3'),
    ("−", '-'),
    (".", '.'),
    ("0", '0'),
    ("", '\b'),
    ("+", '+'),
  };

  /// <param name="actions">Where a pressed key, AC, back and Apply go.</param>
  /// <param name="editing">The variable open: what it can take next lets a key be pressed.</param>
  public KeypadViewModel(IInputDockActions actions, FormulaEditing editing)
  {
    KeypadKeyViewModel[] keys = new KeypadKeyViewModel[Layout.Length];
    for (int i = 0; i < keys.Length; i++)
    {
      (string label, char key) = Layout[i];
      keys[i] = Own(
        new KeypadKeyViewModel(
          "Key " + (key == '\b' ? "backspace" : label),
          label,
          key,
          // The last column, the operators, reads as a group.
          i % 4
            == 3,
          () => actions.Press(key),
          () => CanPress(editing, key)
        )
      );
    }
    Keys = keys;
    Clear = Own(new KeypadKeyViewModel("Key clear", "AC", '\0', false, actions.Clear, () => editing.CanClear));
    Back = Own(
      new KeypadKeyViewModel("Key back", L("위로", "Back"), '\0', false, actions.Back, () => editing.Path.Count > 1)
    );
    Done = Own(new KeypadKeyViewModel("Key done", L("적용", "Apply"), '\0', false, actions.Done, () => editing.Active));
  }

  /// <summary>The sixteen keys of the grid, row by row: digits, the point, backspace and the operators.</summary>
  public readonly IReadOnlyList<KeypadKeyViewModel> Keys;

  /// <summary>AC: empties what is open; nothing is kept until it fits again.</summary>
  public readonly KeypadKeyViewModel Clear;

  /// <summary>Back out of a condition's slot or a group.</summary>
  public readonly KeypadKeyViewModel Back;

  /// <summary>Apply: the way out of the dock, once what is open fits its field.</summary>
  public readonly KeypadKeyViewModel Done;

  // A key can be pressed while it would do something: backspace while there
  // is something to take back, the rest while the variable takes them.
  [Pure]
  private static bool CanPress(FormulaEditing editing, char key) =>
    key == '\b' ? editing.CanBackspace : editing.CanPress(key);

  /// <summary>
  /// Keyboard keys that act like the keypad: digits, point, + - * /,
  /// backspace, and in the operator slot &lt; &gt; = ! (&lt; or &gt; then =
  /// make ≤ or ≥).
  /// </summary>
  public static bool TryKeyboard(Event e, FormulaEditing editing, out char key)
  {
    key = '\0';
    if (e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete)
    {
      key = '\b';
      return true;
    }
    char c = e.character;
    if (char.IsDigit(c) || c == '.' || c == ',')
    {
      key = c == ',' ? '.' : c;
      return true;
    }
    if (editing != null && editing.Active && editing.Kind == OverlayValueType.Compare)
    {
      OverlayPiece current = editing.Working.Count == 1 ? editing.Working[0] : null;
      key = c switch
      {
        '<' or '>' => c,
        '!' => '≠',
        '=' when current?.Op == '<' => '≤',
        '=' when current?.Op == '>' => '≥',
        '=' => '=',
        _ => '\0',
      };
      return key != '\0';
    }
    char op = OverlayVariable.NormalizeOperator(c);
    if (op == '\0')
      return false;
    key = op;
    return true;
  }
}
