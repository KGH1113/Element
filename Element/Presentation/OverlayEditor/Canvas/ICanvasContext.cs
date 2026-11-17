using Element.Presentation.OverlayEditor.Canvas.AddMenu;

namespace Element.Presentation.OverlayEditor.Canvas;

/// <summary>
/// What the layout editor's parts and element kinds read from it: the
/// canvas being arranged, what is selected, the status line and the add
/// menu. What they ask it to do goes the other way, through <see cref="ICanvasActions"/>.
/// </summary>
internal interface ICanvasContext
{
  CanvasSession Session { get; }
  CanvasSelection Selection { get; }
  CanvasStatusLine Status { get; }
  AddMenuPanel AddMenu { get; }
}
