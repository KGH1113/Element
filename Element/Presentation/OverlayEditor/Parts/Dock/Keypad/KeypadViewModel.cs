using System.Collections.Generic;
using Element.Domain.Overlay.Formula;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.Graphics;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>
/// Large keys for a formula: 0–9, a point, + − × ÷, backspace, the six
/// comparisons, "then" (which says "otherwise" after a case's result), AC to
/// start over and Apply. A key that would do nothing can't be pressed and
/// reads dimmed; each key follows the formula being typed by itself. A
/// pressed key goes up as the dock's <see cref="IInputDockActions"/>.
/// <see cref="KeypadView"/> draws it.
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

  private static readonly (char Key, UiIconKind Icon)[] Comparisons =
  {
    ('<', UiIconKind.Less),
    ('≤', UiIconKind.LessOrSame),
    ('=', UiIconKind.Same),
    ('≠', UiIconKind.NotSame),
    ('≥', UiIconKind.MoreOrSame),
    ('>', UiIconKind.More),
  };

  /// <param name="actions">Where a pressed key, AC and Done go.</param>
  /// <param name="editing">The formula typed: what it can take next lets a key be pressed.</param>
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
    KeypadKeyViewModel[] compares = new KeypadKeyViewModel[Comparisons.Length];
    for (int i = 0; i < compares.Length; i++)
    {
      (char key, UiIconKind icon) = Comparisons[i];
      compares[i] = Own(
        new KeypadKeyViewModel(
          "Key " + key,
          key.ToString(),
          key,
          true,
          () => actions.Press(key),
          () => CanPress(editing, key),
          icon: icon
        )
      );
    }
    Compares = compares;
    string then = L("이면", "then"),
      otherwise = L("아니면", "otherwise");
    Next = Own(
      new KeypadKeyViewModel(
        "Key then",
        then,
        OverlayFormulaKeypad.Next,
        true,
        () => actions.Press(OverlayFormulaKeypad.Next),
        () => CanPress(editing, OverlayFormulaKeypad.Next),
        () => editing.NextStep == OverlayFormulaTokenKind.Else ? otherwise : then
      )
    );
    Clear = Own(new KeypadKeyViewModel("Key clear", "AC", '\0', false, actions.Clear, () => editing.CanClear));
    Done = Own(new KeypadKeyViewModel("Key done", L("적용", "Apply"), '\0', false, actions.Done, () => editing.Active));
  }

  /// <summary>The sixteen keys of the grid, row by row: digits, the point, backspace and the operators.</summary>
  public readonly IReadOnlyList<KeypadKeyViewModel> Keys;

  /// <summary>The short row over the grid: &lt; ≤ = ≠ ≥ &gt;.</summary>
  public readonly IReadOnlyList<KeypadKeyViewModel> Compares;

  /// <summary>"Then" after a comparison, "otherwise" after a case's result.</summary>
  public readonly KeypadKeyViewModel Next;

  /// <summary>AC: starts over; nothing is written until something new is typed.</summary>
  public readonly KeypadKeyViewModel Clear;

  /// <summary>Done: the way out of the keypad.</summary>
  public readonly KeypadKeyViewModel Done;

  // A key can be pressed while it would do something to the formula: backspace
  // while there is something to take back, the rest while the formula takes them.
  [Pure]
  private static bool CanPress(FormulaEditing editing, char key) =>
    key == '\b' ? editing.CanBackspace : editing.CanPress(key);

  /// <summary>
  /// Keyboard keys that act like the keypad: digits, point, + - * /,
  /// backspace, and &lt; &gt; = ! ? for the comparisons and "then"; &lt; or
  /// &gt; then = make ≤ or ≥ in <paramref name="editing"/>.
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
    char compare = OverlayFormulaKeypad.FromKeyboard(editing?.Working, c);
    if (compare != '\0')
    {
      key = compare;
      return true;
    }
    char op = OverlayFormula.NormalizeOperator(c);
    if (op == '\0')
      return false;
    key = op;
    return true;
  }
}
