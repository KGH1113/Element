using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Graphics;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>One key: what it says, what it types, and whether it can be pressed now.</summary>
internal sealed class KeypadKeyViewModel : ViewModel
{
  /// <param name="name">Names the drawn button, for the checks: "Key 7", "Key done".</param>
  /// <param name="label">What it says; empty for backspace, which shows an icon.</param>
  /// <param name="character">What it types; '\b' for backspace, '\0' for AC and Done.</param>
  /// <param name="symbol">An operator, which reads in the accent.</param>
  /// <param name="enabled">Whether pressing it would do something now; followed as it changes.</param>
  /// <param name="saying">What it says when that follows the formula, as "then" turns into "otherwise"; null for <paramref name="label"/>.</param>
  public KeypadKeyViewModel(
    string name,
    string label,
    char character,
    bool symbol,
    Action press,
    Expression<Func<bool>> enabled,
    Expression<Func<string>> saying = null,
    UiIconKind? icon = null
  )
  {
    Icon = icon;
    Name = name;
    Label = label;
    Character = character;
    Symbol = symbol;
    Enabled = Computed(enabled);
    Press = Command(press, Enabled);
    Saying = saying == null ? null : Computed(saying);
  }

  public readonly string Name;

  /// <summary>Drawn instead of its label; null for a key that says its label.</summary>
  public readonly UiIconKind? Icon;
  public readonly string Label;

  /// <summary>What it says while that follows the formula; null when it always says <see cref="Label"/>.</summary>
  public readonly Computed<string> Saying;
  public readonly char Character;
  public readonly bool Symbol;

  /// <summary>Whether pressing it would do something now.</summary>
  public readonly Computed<bool> Enabled;

  public Command Press { get; }
}
