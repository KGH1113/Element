using Element.Domain.Overlay.Values;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// Everything the input dock asks for, and the only way out of it: a key
/// typed, AC, Done, a value clicked, and a value carried out of the dock. The
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

  /// <summary>A value starts being carried out of the dock, at a screen point.</summary>
  void BeginDrag(OverlayValueDefinition value, Vector2 screen);

  /// <summary>The carried value moved to a screen point.</summary>
  void Drag(Vector2 screen);

  /// <summary>The carried value was let go at a screen point.</summary>
  void EndDrag(Vector2 screen);
}
