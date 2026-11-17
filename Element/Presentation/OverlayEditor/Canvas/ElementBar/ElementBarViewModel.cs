using Element.Application.Layout;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Canvas.ElementBar;

/// <summary>
/// What the element toolbar shows for the selection: align and distribute
/// for several, or what one element's kind offers (hand or foot for a key
/// viewer). The toolbar keeps it while the selection stays the same.
/// </summary>
internal abstract class ElementBarViewModel : ViewModel
{
  /// <summary>The one element it is for; null for several.</summary>
  public virtual IOverlayLayoutTarget Target => null;

  /// <summary>Once a frame while it shows: follow what changed elsewhere.</summary>
  public virtual void Sync() { }

  /// <summary>A control on the bar wrote a value.</summary>
  public virtual void Edited() { }
}
