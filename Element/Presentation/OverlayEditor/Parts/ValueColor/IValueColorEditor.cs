using Element.Configuration.Overlay;

namespace Element.Presentation.OverlayEditor.Parts.ValueColor;

/// <summary>
/// What the controls of a color that follows a value ask of the editor whose
/// color it is: the band's stops, the picked stop's value and color, and
/// which value it follows. The editor makes each change, keeps it for undo
/// and shows the result again (<see cref="ValueColorViewModel.Show"/>).
/// </summary>
internal interface IValueColorEditor
{
  /// <summary>The value row: offer the values to follow.</summary>
  void PickSource();

  /// <summary>The picked stop's value: type it in the dock.</summary>
  void EditStopValue();

  /// <summary>The picked stop's color: open the color picker on it.</summary>
  void PickStopColor();

  /// <summary>Remove the picked stop.</summary>
  void RemoveStop();

  void SetMode(OverlayValueColorMode mode);

  void SelectStop(int index);

  /// <summary>Adds a stop at <paramref name="at"/> with the color the band has there and picks it; its index, or -1 when it can't.</summary>
  int AddStop(float at);

  /// <summary>A stop starts being dragged along the band.</summary>
  void BeginDrag(int index);

  /// <summary>The dragged stop is now at <paramref name="at"/>.</summary>
  void DragStop(int index, float at);

  /// <summary>The drag ended.</summary>
  void EndDrag();
}
