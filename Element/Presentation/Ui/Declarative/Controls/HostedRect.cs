using System;
using System.Linq.Expressions;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A RectTransform someone else made and keeps, shown where this view is laid
/// out: a slot its owner fills with a control of its own, or a hand-drawn
/// part that outlives the screen. While laid out, the rect sits in the view at
/// its size; when the screen goes away it goes back home. Two such views may
/// show the same rect, one at a time (in the two branches of an <c>If</c>): it
/// follows the one laid out last.
/// </summary>
internal sealed class HostedRect : NativeView
{
  private readonly RectTransform _target;
  private readonly Transform _home;
  private readonly float _width;
  private readonly Expression<Func<float>> _height;
  private readonly Action<float> _resized;
  private float _shownHeight;

  /// <param name="target">The rect to show.</param>
  /// <param name="home">Where it goes back to, such as a hidden holder of its owner.</param>
  /// <param name="width">Its width; NaN takes the width offered.</param>
  /// <param name="height">Its height.</param>
  public HostedRect(RectTransform target, Transform home, float width, float height)
  {
    _target = target;
    _home = home;
    _width = width;
    _shownHeight = height;
  }

  /// <param name="height">Its height, which follows the view model.</param>
  /// <param name="resized">Lays the rect's contents out at a new width.</param>
  public HostedRect(
    RectTransform target,
    Transform home,
    float width,
    Expression<Func<float>> height,
    Action<float> resized
  )
    : this(target, home, width, 0)
  {
    _height = height;
    _resized = resized;
  }

  protected override void Build(RectTransform rect)
  {
    if (_height != null)
      Watch(
        _height,
        height =>
        {
          _shownHeight = height;
          SizeChanged();
        }
      );
  }

  protected override Size PreferredSize(Proposal proposal)
  {
    float width =
      !float.IsNaN(_width) ? _width
      : proposal.HasWidth && !float.IsInfinity(proposal.Width) ? proposal.Width
      : 0;
    return new Size(width, _shownHeight);
  }

  protected override void Resized(float width, float height)
  {
    if (_target == null)
      return;
    if (_target.parent != Rect)
      _target.SetParent(Rect, false);
    _target.anchorMin = _target.anchorMax = _target.pivot = new Vector2(0, 1);
    _target.anchoredPosition = Vector2.zero;
    _target.sizeDelta = new Vector2(width, height);
    if (!_target.gameObject.activeSelf)
      _target.gameObject.SetActive(true);
    _resized?.Invoke(width);
  }

  protected override void Teardown()
  {
    if (_target != null && _home != null && _target.parent == Rect)
      _target.SetParent(_home, false);
  }
}
