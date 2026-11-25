using Element.Domain.Overlay.Values;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Everything the input dock asks for, and the only way out of it: a key
/// typed, AC, Done, a value clicked, and a value let go on its line. The
/// editor that shows the dock answers each one; the dock never changes what
/// is edited itself, it shows what the editor tells it
/// (<see cref="InputDockViewModel.Show"/>).
/// </summary>
internal interface IInputDockActions
{
  /// <summary>A key typed on the keypad: a digit, the point, an operator, or '\b' for backspace.</summary>
  void Press(char key);

  /// <summary>AC: the formula starts over.</summary>
  void Clear();

  /// <summary>Done: what is typed is kept, and the dock rests.</summary>
  void Done();

  /// <summary>A value clicked, or found by the search and picked with Enter.</summary>
  void Pick(OverlayValueDefinition value);

  /// <summary>
  /// A value started being carried out of the dock: the editor puts away what
  /// would cover the places it can land, such as a value's name tip.
  /// </summary>
  void BeganCarrying();

  /// <summary>
  /// A value carried from the dock was let go on its formula line: it joins
  /// the formula, or, words where a number goes, the editor says why not.
  /// </summary>
  void DropOnFormula(OverlayValueDefinition value);
}
