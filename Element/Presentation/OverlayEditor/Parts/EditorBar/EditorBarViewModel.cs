using System.Collections.Generic;
using Element.Presentation.OverlayEditor.Parts.Property;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.EditorBar;

/// <summary>The buttons of an editor's bar, in order; they go with the bar.</summary>
internal sealed class EditorBarViewModel : ViewModel
{
  public EditorBarViewModel(IReadOnlyList<PropertyButtonViewModel> buttons)
  {
    foreach (PropertyButtonViewModel button in buttons)
      Own(button);
    Buttons = buttons;
  }

  public readonly IReadOnlyList<PropertyButtonViewModel> Buttons;

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
