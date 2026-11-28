using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts.Formula;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Everything the input dock asks for, and the only way out of it: a key
/// typed, a piece picked on the line, a piece's ×, a value clicked, and a
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

  /// <summary>The switch input method: the picked true or false becomes <paramref name="value"/>.</summary>
  void Switch(bool value);

  /// <summary>The nine points: the picked point becomes <paramref name="point"/>.</summary>
  void Point(OverlayGrowth point);

  /// <summary>The picked value's own number: it is read at <paramref name="digits"/> decimals.</summary>
  void Digits(int digits);

  /// <summary>
  /// A slot of the picked condition clicked in its input method: the dock
  /// goes into it, its line and input method that slot's from then on.
  /// </summary>
  void Enter(int slot);

  /// <summary>
  /// Another slot of the condition the dock is inside, clicked on its folded
  /// strip: the dock goes straight there, without coming out in between.
  /// </summary>
  void Hop(int slot);

  /// <summary>Back: one step out of the slot the dock is in, to the condition it is a slot of.</summary>
  void Leave();

  /// <summary>The × on the picked piece: it is taken out.</summary>
  void Remove();

  /// <summary>A value clicked, or found by the search and picked with Enter.</summary>
  void Pick(OverlayValueDefinition value);
}
