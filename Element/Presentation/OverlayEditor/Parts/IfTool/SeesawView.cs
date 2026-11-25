using System;
using System.Linq.Expressions;
using Element.Domain.Overlay.Conditions;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Motion = ElementUI.Motion;

namespace Element.Presentation.OverlayEditor.Parts.IfTool;

/// <summary>
/// A small seesaw with the value (a dot) on its left seat and what it is
/// held against (a box) on its right. The bigger side sinks: tilted right
/// when the value is smaller, level when they are the same, tilted left when
/// it is bigger. It rests level and faint while there is nothing to compare.
/// Its enclosing control owns the click.
/// </summary>
internal sealed class SeesawView : ComponentView
{
  private const float Tilt = 12;
  private readonly Computed<OverlayComparison> _result;
  private readonly Expression<Func<UiColor>> _ink;
  private readonly float _width;

  /// <param name="result">How the value compares; a fixed one draws a still picture of that result.</param>
  /// <param name="ink">The plank's and the box's color.</param>
  public SeesawView(Expression<Func<OverlayComparison>> result, float width, Expression<Func<UiColor>> ink)
  {
    _result = Computed(result);
    _ink = ink;
    _width = width;
  }

  /// <summary>How tall a seesaw <paramref name="width"/> wide is.</summary>
  public static float HeightFor(float width) => Mathf.Round(width * .56f);

  public override View Body
  {
    get
    {
      float seat = Mathf.Round(_width * .2f),
        plank = Mathf.Max(2, Mathf.Round(_width * .06f)),
        stand = Mathf.Round(_width * .16f);
      View seats = HStack(
          spacing: 0,
          Circle().Fill((UiColor)UiPalette.SoftAccent).Frame(width: seat, height: seat),
          Spacer(),
          RoundedRectangle(seat * .25f).Fill(_ink).Frame(width: seat, height: seat)
        )
        .Padding(new Insets(0, 2, 0, 2));
      return VStack(
          0,
          HAlignment.Center,
          VStack(spacing: 1, seats, Capsule().Fill(_ink).Frame(height: plank))
            .Frame(width: _width)
            .RotationEffect(
              () =>
                _result.Value == OverlayComparison.Smaller ? Tilt
                : _result.Value == OverlayComparison.Bigger ? -Tilt
                : 0f,
              Alignment.Bottom
            )
            .Animation(Motion.EaseOut(.22f), () => _result.Value),
          Circle().Fill((UiColor)UiPalette.Muted).Frame(width: stand, height: stand)
        )
        .Frame(width: _width, height: HeightFor(_width), alignment: Alignment.Bottom)
        .Opacity(() => _result.Value == OverlayComparison.Unknown ? .45f : 1f)
        .AllowsHitTesting(false);
    }
  }
}
