using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Tells a view model the width it is laid out in, for a layout that changes
/// with the room it has: put it in a <c>ZStack</c> with that layout and pick
/// the layout in an <c>If</c> on the width. It draws nothing and takes no height.
/// A new width shows from the next frame.
/// </summary>
internal sealed class WidthReader : NativeView
{
  private readonly Binding<float> _width;

  /// <param name="width">Where the width goes, such as <c>Bind(() =&gt; VM.Width)</c>.</param>
  public WidthReader(Binding<float> width) => _width = width;

  protected override void Build(RectTransform rect) { }

  protected override Size PreferredSize(Proposal proposal) =>
    new(float.IsNaN(proposal.Width) || float.IsInfinity(proposal.Width) ? 0 : proposal.Width, 0);

  protected override void Resized(float width, float height)
  {
    if (width > 0 && Mathf.Abs(_width.Value - width) >= .5f)
      _width.Value = width;
  }
}
