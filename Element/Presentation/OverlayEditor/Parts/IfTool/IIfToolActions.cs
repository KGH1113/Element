using Element.Domain.Overlay.Conditions;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// What the If tool asks of the editor whose text it is. The editor makes
/// each change, keeps it for undo and shows the result again
/// (<see cref="IfToolViewModel"/>'s cases).
/// </summary>
internal interface IIfToolActions
{
  /// <summary>Add a case; the first one makes the selection, or the whole text, an If.</summary>
  void AddCase();

  /// <summary>Take case <paramref name="index"/> and its look away.</summary>
  void RemoveCase(int index);

  /// <summary>The value chip of case <paramref name="index"/>: offer the values to compare.</summary>
  void PickValue(int index);

  /// <summary>The number of case <paramref name="index"/>: type it in the dock.</summary>
  void EditNumber(int index);

  /// <summary>Whether case <paramref name="index"/> is on when its value compares as <paramref name="result"/>: flip it.</summary>
  void Toggle(int index, OverlayComparison result);

  /// <summary>Put look <paramref name="look"/> (a case's, or the count for otherwise) in the preview to see and edit.</summary>
  void ShowLook(int look);

  /// <summary>The try-a-value slider moved to <paramref name="position"/>, from 0 to 1 along its range.</summary>
  void Try(float position);
}
