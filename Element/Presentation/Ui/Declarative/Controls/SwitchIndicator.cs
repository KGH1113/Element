using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Theme;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;
using Motion = ElementUI.Motion;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>A noninteractive three-state switch. Its enclosing row owns the click.</summary>
internal sealed class SwitchIndicator : ComponentView
{
  private const float Width = 30,
    Height = 18,
    Inset = 4,
    Thumb = 10;
  private readonly Computed<bool?> _on;
  private readonly Computed<bool> _available;

  public SwitchIndicator(Expression<Func<bool?>> on, Expression<Func<bool>> available)
  {
    _on = Computed(on);
    _available = Computed(available);
  }

  public override View Body
  {
    get
    {
      UiColor off = new Color(.44f, .44f, .46f),
        on = UiPalette.AccentSurface;
      UiColor thumbOff = UiPalette.Foreground,
        thumbOn = UiPalette.SoftAccent;
      return Capsule()
        .Fill(() => _on.Value == false ? off : on)
        .Frame(width: Width, height: Height)
        .Overlay(
          Circle()
            .Fill(() => _on.Value == false ? thumbOff : thumbOn)
            .WithoutAnimation()
            .Frame(width: Thumb, height: Thumb)
            .Offset(x: () =>
              Inset
              + (
                _on.Value == null ? 6
                : _on.Value.Value ? 12
                : 0
              )
            )
            .Animation(Motion.Exponential(24, .05f), () => _on.Value),
          Alignment.Leading
        )
        .Opacity(() => _available.Value ? 1 : .4f)
        .AllowsHitTesting(false);
    }
  }
}
