using System;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.Canvas.Layers;

/// <summary>
/// A card's picture: a dark rounded tile with the elements drawn small in
/// it, drawn again at the size layout gives it and let go with the card.
/// </summary>
internal sealed class LayerPictureView : NativeView
{
  private static readonly Color Tile = new(.045f, .055f, .08f);

  private readonly Func<RectTransform, IDisposable> _fill;
  private readonly float _width,
    _height;
  private RectTransform _rect;
  private IDisposable _picture;
  private Vector2 _drawn;

  /// <param name="fill">Draws the elements into the tile until the returned object is disposed.</param>
  public LayerPictureView(Func<RectTransform, IDisposable> fill, float width, float height)
  {
    _fill = fill;
    _width = width;
    _height = height;
  }

  protected override void Build(RectTransform rect)
  {
    _rect = rect;
    RoundedRect tile = rect.gameObject.AddComponent<RoundedRect>();
    tile.Radius = 5;
    tile.color = Tile;
    tile.raycastTarget = false;
  }

  protected override Size PreferredSize(Proposal proposal) => new(_width, _height);

  protected override void Resized(float width, float height)
  {
    Vector2 size = new(width, height);
    if (_picture != null && size == _drawn)
      return;
    _drawn = size;
    _picture?.Dispose();
    _picture = _rect != null && width > 0 && height > 0 ? _fill(_rect) : null;
  }

  protected override void Teardown()
  {
    _picture?.Dispose();
    _picture = null;
  }
}
