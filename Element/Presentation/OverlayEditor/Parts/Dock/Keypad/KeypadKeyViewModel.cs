using System;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Keypad;

/// <summary>One key: what it says, what it types, and whether it can be pressed now.</summary>
internal sealed class KeypadKeyViewModel : ViewModel
{
  /// <param name="name">Names the drawn button, for the checks: "Key 7", "Key done".</param>
  /// <param name="label">What it says; empty for backspace, which shows an icon.</param>
  /// <param name="character">What it types; '\b' for backspace, '\0' for AC and Done.</param>
  /// <param name="symbol">An operator, which reads in the accent.</param>
  public KeypadKeyViewModel(string name, string label, char character, bool symbol, Action press)
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
