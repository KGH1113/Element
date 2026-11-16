using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

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
      view = ZStack(Alignment.TopLeading, view, new ModPartRect(part));
    if (part.HintText.Value != null)
      view = view.Hint(() => part.HintText.Value);
    return view.Hidden(() => part.Hidden.Value);
  }
}

/// <summary>
/// The object a mod got from a part's <c>Rect</c>, spread over the part
/// wherever it is shown; between mounts the page keeps it.
/// </summary>
internal sealed class ModPartRect : NativeView
{
  private readonly ModPart _part;

  public ModPartRect(ModPart part) => _part = part;

  protected override void Build(RectTransform rect)
  {
    RectTransform own = _part.OwnRect;
    if (own == null)
      return;
    own.SetParent(rect, false);
    RectPeer.Stretch(own, 0, 0, 0, 0);
  }

  // As large as the part it covers; it adds nothing to its size.
  protected override Size PreferredSize(Proposal proposal) => new(Finite(proposal.Width), Finite(proposal.Height));

  private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : value;

  protected override void Teardown() => _part.Park();
}
