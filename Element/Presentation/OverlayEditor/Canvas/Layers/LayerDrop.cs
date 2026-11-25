using System.Collections.Generic;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// Where a carried card lands: above the first card whose middle is below
/// the pointer, or at the very back below all of them. Read from where the
/// cards are drawn, so a scrolled or scaled list still lands right.
/// </summary>
internal sealed class LayerDrop : IDropDestination<LayerDrag>
{
  private readonly LayersViewModel _list;
  private readonly IReadOnlyDictionary<string, RectTransform> _cards;
  private readonly Vector3[] _corners = new Vector3[4];

  /// <param name="cards">Each shown card's frame by its key.</param>
  public LayerDrop(LayersViewModel list, IReadOnlyDictionary<string, RectTransform> cards)
  {
    _list = list;
    _cards = cards;
  }

  public DropState Over(LayerDrag value, Vector2 position)
  {
    _list.Over(value, Before(position));
    return DropState.Accepts;
  }

  public void Drop(LayerDrag value, Vector2 position) => _list.Drop(value, Before(position));

  public void Refused(LayerDrag value, Vector2 position) { }

  public void Left() => _list.Leave();

  // The first card, front-most first, whose middle is below the pointer (origin bottom left).
  private int Before(Vector2 position)
  {
    LayerRowViewModel[] rows = _list.Rows;
    for (int i = 0; i < rows.Length; i++)
    {
      if (!_cards.TryGetValue(rows[i].Key, out RectTransform card) || card == null)
        continue;
      card.GetWorldCorners(_corners);
      float middle = (_corners[0].y + _corners[1].y) * .5f;
      if (middle < position.y)
        return i;
    }
    return rows.Length;
  }
}
