using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Nothing to see: it takes the width it is offered and writes it to a state,
/// so a page can pick how many columns fit, as <c>If(() =&gt; width.Value &gt;= 600, ...)</c>.
/// Put it beside the content in a <c>ZStack</c>, where it takes no height.
/// </summary>
/// <remarks>
/// ElementUI has no layout that changes its shape with the width it gets
/// (columns that drop to one on a narrow window), and a layout of one's own
/// can't place views. The width arrives after a layout, so a page that crosses
/// a breakpoint shows its new shape one frame later.
/// </remarks>
internal sealed class WidthReader : NativeView
{
  private readonly State<float> _width;

  public WidthReader(State<float> width) => _width = width;

  protected override void Build(RectTransform rect) { }

  protected override Size PreferredSize(Proposal proposal) =>
    new(float.IsNaN(proposal.Width) || float.IsInfinity(proposal.Width) ? 0 : proposal.Width, 0);

  protected override void Resized(float width, float height)
  {
    if (width > 0)
      _width.Value = width;
  }
}
