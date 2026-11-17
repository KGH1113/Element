using System.Collections.Generic;
using Element.Domain.Overlay.Formula;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell.Dock.Keypad;

/// <summary>
/// Large keys for a formula: 0–9, a point, + − × ÷, backspace, AC to start
/// over and Done. A key that would do nothing can't be pressed and reads
/// dimmed; the dock says which with <see cref="Show"/>. A pressed key goes
/// up as the dock's <see cref="IInputDockActions"/>. <see cref="KeypadView"/>
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

  /// <param name="actions">Where a pressed key, AC and Done go.</param>
  public KeypadViewModel(IInputDockActions actions)
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
          () => actions.Press(key)
        )
      );
    }
    Keys = keys;
    Clear = Own(new KeypadKeyViewModel("Key clear", "AC", '\0', false, actions.Clear));
    Done = Own(new KeypadKeyViewModel("Key done", L("적용", "Apply"), '\0', false, actions.Done));
  }

  /// <summary>The sixteen keys of the grid, row by row: digits, the point, backspace and the operators.</summary>
  public readonly IReadOnlyList<KeypadKeyViewModel> Keys;

  /// <summary>AC: starts over; nothing is written until something new is typed.</summary>
  public readonly KeypadKeyViewModel Clear;

  /// <summary>Done: the way out of the keypad.</summary>
  public readonly KeypadKeyViewModel Done;

  /// <summary>Lets the keys that would do something now be pressed, and dims the others.</summary>
  public void Show(OverlayFormulaEditing editing)
  {
    for (int i = 0; i < Keys.Count; i++)
    {
      KeypadKeyViewModel key = Keys[i];
      key.Enabled.Value = key.Character == '\b' ? editing.CanBackspace : editing.CanPress(key.Character);
    }
    Clear.Enabled.Value = editing.CanClear;
    Done.Enabled.Value = editing.Active;
  }

  /// <summary>Keyboard keys that act like the keypad: digits, point, + - * / and backspace.</summary>
  public static bool TryKeyboard(Event e, out char key)
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
    char op = OverlayFormula.NormalizeOperator(c);
    if (op == '\0')
      return false;
    key = op;
    return true;
  }
}
