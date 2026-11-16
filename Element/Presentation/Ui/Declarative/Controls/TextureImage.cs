using System;
using System.Linq.Expressions;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A texture shown whole in the room it is given, centered and never
/// stretched: a render texture such as the planet preview. It follows the
/// texture its expression picks; nothing shows while that is null. It takes
/// the room it is offered, so give it a <c>Frame</c>.
/// </summary>
internal sealed class TextureImage : NativeView
{
  private readonly Expression<Func<Texture>> _texture;
  private RawImage _image;
  private float _width,
    _height;

  /// <param name="texture">The texture to show, such as <c>() =&gt; VM.Preview</c>.</param>
  public TextureImage(Expression<Func<Texture>> texture) => _texture = texture;

  protected override void Build(RectTransform rect)
  {
    RectTransform child = (RectTransform)new GameObject("Texture", typeof(RectTransform)).transform;
    child.gameObject.layer = rect.gameObject.layer;
    child.SetParent(rect, false);
    child.anchorMin = child.anchorMax = child.pivot = new Vector2(.5f, .5f);
    _image = child.gameObject.AddComponent<RawImage>();
    _image.raycastTarget = false;
    _image.enabled = false;
    Watch(_texture, Show);
  }

  private void Show(Texture texture)
  {
    _image.texture = texture;
    _image.enabled = texture != null;
    Fit();
  }

  protected override Size PreferredSize(Proposal proposal) =>
    new(Room(proposal.Width, 160), Room(proposal.Height, 100));

  private static float Room(float offered, float ideal) =>
    float.IsNaN(offered) || float.IsInfinity(offered) ? ideal : offered;

  protected override void Resized(float width, float height)
  {
    _width = width;
    _height = height;
    Fit();
  }

  // The largest size with the texture's shape that fits the room.
  private void Fit()
  {
    Texture texture = _image.texture;
    float aspect = texture != null && texture.height > 0 ? texture.width / (float)texture.height : 1;
    float width = Mathf.Max(0, Mathf.Min(_width, _height * aspect));
    _image.rectTransform.sizeDelta = new Vector2(width, width / aspect);
  }
}
