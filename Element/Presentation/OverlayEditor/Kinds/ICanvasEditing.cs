using Element.Application.Layout;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>What an element kind may ask of the layout editor.</summary>
internal interface ICanvasEditing
{
  CanvasSession Session { get; }
  CanvasSelection Selection { get; }
  CanvasStatusLine Status { get; }
  AddMenuPanel AddMenu { get; }

  /// <summary>
  /// Before a full-screen editor or a dialog: applies typed popover values,
  /// closes the popovers and ends drags. False when a typed value can't be
  /// used; nothing else changes then.
  /// </summary>
  bool Settle();

  /// <summary>A new element in the middle of the view, selected; one undo step with <paramref name="commit"/>.</summary>
  void PlaceNew(IOverlayLayoutTarget target, bool commit);

  /// <summary>Places every element again from its settings.</summary>
  void RefreshLayouts();

  /// <summary>Updates the outlines, toolbars and popovers to what changed.</summary>
  void UpdateFrames();
}
