using Element.Application.Layout;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Canvas.ElementBar;

/// <summary>An element toolbar's contents: its view model and the view drawn from it.</summary>
internal sealed class ElementBarContent
{
  public ElementBarContent(ElementBarViewModel model, View view)
  {
    Model = model;
    View = view;
  }

  public ElementBarViewModel Model { get; }

  public View View { get; }
}
