using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// A part as the page shows it: its control, hidden (keeping its place)
/// while the mod hides it, with its hint over the whole row, and the object
/// the mod asked for with <c>Rect</c> spread over it.
/// </summary>
internal sealed class ModPartView : View<ModPart>
{
  public ModPartView(ModPart part)
    : base(part) { }

  public override View Body => Dressed(VM);

  private static View Dressed(ModPart part)
  {
    View view = part.Control?.Invoke() ?? Spacer().Frame(height: 0);
    if (part.OwnRect != null)
      view = ZStack(Alignment.TopLeading, view, new ModPartRectView(part));
    if (part.HintText.Value != null)
      view = view.Hint(() => part.HintText.Value);
    return view.Hidden(() => part.Hidden.Value);
  }
}
