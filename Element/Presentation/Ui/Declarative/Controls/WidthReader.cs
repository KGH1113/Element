using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Draws nothing; writes the width it is laid out at into a view model value.
/// A stopgap from before ElementUI had <c>Grid(minColumnWidth:)</c> and
/// <c>ViewThatFits</c>; screens move to those.
/// </summary>
internal sealed class WidthReader : NativeView
{
  private readonly Binding<float> _binding;
  private readonly State<float> _state;

  public WidthReader(Binding<float> width) => _binding = width;

  public WidthReader(State<float> width) => _state = width;

  protected override void Build(RectTransform rect) { }

  protected override Size PreferredSize(Proposal proposal) =>
    new(float.IsNaN(proposal.Width) || float.IsInfinity(proposal.Width) ? 0 : proposal.Width, 0);

  protected override void Resized(float width, float height)
  {
    if (width <= 0)
      return;
    if (_state != null)
    {
      if (Mathf.Abs(_state.Value - width) >= .5f)
        _state.Value = width;
    }
    else if (Mathf.Abs(_binding.Value - width) >= .5f)
      _binding.Value = width;
  }
}
