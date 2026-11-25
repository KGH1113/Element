using System.Collections.Generic;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Formula;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Everything the input dock asks for, and the only way out of it: a key
/// typed, a piece picked on the line, Remove, Apply, a value clicked, and a
/// value let go on its line. The
/// editor that shows the dock answers each one; the dock never changes what
/// is edited itself, it shows what the editor tells it
/// (<see cref="InputDockViewModel.Show"/>).
/// </summary>
internal interface IInputDockActions
{
  /// <summary>A key typed on the keypad: a digit, the point, an operator, a comparison, or '\b' for backspace.</summary>
  void Press(char key);

  /// <summary>
  /// Something on the line clicked: piece <paramref name="index"/> of the
  /// variable <paramref name="route"/> leads to, or with -1 that variable
  /// itself (an empty slot, where a value then goes).
  /// </summary>
  void SelectAt(IReadOnlyList<FormulaStep> route, int index);

  /// <summary>The keyboard input method typed: the picked words become <paramref name="words"/>.</summary>
  void Words(string words);

  /// <summary>The picked value of the player's is called <paramref name="name"/> from now on.</summary>
  void Rename(string name);

  /// <summary>The switch input method: the picked true or false becomes <paramref name="value"/>.</summary>
  void Switch(bool value);

  /// <summary>The picked value of the player's opens: its inside is on the line, to change.</summary>
  void OpenMade();

  /// <summary>지우기: the picked value is taken out.</summary>
  void Remove();

  /// <summary>Done: what is typed is kept, and the dock rests.</summary>
  void Done();

  /// <summary>A value clicked, or found by the search and picked with Enter.</summary>
  void Pick(OverlayValueDefinition value);
}
