using System;
using System.Collections.Generic;
using Element.Domain.Overlay.Formula;
using ElementUI;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Large keys for a formula: 0–9, a point, + − × ÷, backspace, AC to start
/// over and Done. A key that would do nothing can't be pressed and reads
/// dimmed; the dock says which with <see cref="Refresh"/>.
/// <see cref="OverlayKeypadView"/> draws it.
/// </summary>
internal sealed class OverlayKeypadModel : ViewModel
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

  /// <param name="press">Types a key's character; backspace is '\b'.</param>
  /// <param name="clear">AC: starts the formula over.</param>
  /// <param name="done">Done: lets the dock rest.</param>
  public OverlayKeypadModel(Action<char> press, Action clear, Action done)
  {
    KeypadKeyModel[] keys = new KeypadKeyModel[Layout.Length];
    for (int i = 0; i < keys.Length; i++)
    {
      (string label, char key) = Layout[i];
      keys[i] = Own(
        new KeypadKeyModel(
          "Key " + (key == '\b' ? "backspace" : label),
          label,
          key,
          // The last column, the operators, reads as a group.
          i % 4 == 3,
          () => press(key)
        )
      );
    }
    Keys = keys;
    Clear = Own(new KeypadKeyModel("Key clear", "AC", '\0', false, clear));
    Done = Own(new KeypadKeyModel("Key done", L("적용", "Apply"), '\0', false, done));
  }

  /// <summary>The sixteen keys of the grid, row by row: digits, the point, backspace and the operators.</summary>
  public readonly IReadOnlyList<KeypadKeyModel> Keys;

  /// <summary>AC: starts over; nothing is written until something new is typed.</summary>
  public readonly KeypadKeyModel Clear;

  /// <summary>Done: the way out of the keypad.</summary>
  public readonly KeypadKeyModel Done;

  /// <summary>Lets the keys that would do something now be pressed, and dims the others.</summary>
  public void Refresh(OverlayFormulaEditing editing)
  {
    for (int i = 0; i < Keys.Count; i++)
    {
      KeypadKeyModel key = Keys[i];
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

/// <summary>One key: what it says, what it types, and whether it can be pressed now.</summary>
internal sealed class KeypadKeyModel : ViewModel
{
  /// <param name="name">Names the drawn button, for the checks: "Key 7", "Key done".</param>
  /// <param name="label">What it says; empty for backspace, which shows an icon.</param>
  /// <param name="character">What it types; '\b' for backspace, '\0' for AC and Done.</param>
  /// <param name="symbol">An operator, which reads in the accent.</param>
  public KeypadKeyModel(string name, string label, char character, bool symbol, Action press)
  {
    Name = name;
    Label = label;
    Character = character;
    Symbol = symbol;
    Press = Command(press, Computed(() => Enabled.Value));
  }

  public readonly string Name;
  public readonly string Label;
  public readonly char Character;
  public readonly bool Symbol;

  /// <summary>Whether pressing it would do something now.</summary>
  public readonly State<bool> Enabled = new(false);

  public Command Press { get; }
}
