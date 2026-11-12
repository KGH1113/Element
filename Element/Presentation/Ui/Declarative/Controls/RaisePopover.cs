using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Nothing to see: put it in a popover's content and the popover draws over the
/// screen. The pinned ElementUI (11316be) makes its popover layer first, so
/// everything made after it, such as a page shown for the first time, draws on
/// top of an open popover; this moves the layer last when the popover opens.
/// ElementUI fc436a1 orders the layer itself; then this can go.
/// </summary>
internal sealed class RaisePopover : NativeView
{
  private const string Layer = "Overlays";

  protected override void Build(RectTransform rect)
  {
    for (Transform parent = rect.parent; parent != null; parent = parent.parent)
      if (parent.name == Layer)
      {
        parent.SetAsLastSibling();
        return;
      }
  }

  protected override Size PreferredSize(Proposal proposal) => new(0, 0);
}
