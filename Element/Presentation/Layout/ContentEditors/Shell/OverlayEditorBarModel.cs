using System.Collections.Generic;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>The buttons of an editor's bar, in order; they go with the bar.</summary>
internal sealed class OverlayEditorBarModel : ViewModel
{
  public OverlayEditorBarModel(IReadOnlyList<OverlayBarButtonModel> buttons)
  {
    foreach (OverlayBarButtonModel button in buttons)
      Own(button);
    Buttons = buttons;
  }

  public readonly IReadOnlyList<OverlayBarButtonModel> Buttons;

  /// <summary>Goes up each time a button's shown value changes, which changes the bar's width.</summary>
  public int Revision
  {
    get
    {
      int revision = 0;
      for (int i = 0; i < Buttons.Count; i++)
        revision += Buttons[i].Revision;
      return revision;
    }
  }
}
