using System;
using System.Linq.Expressions;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using ElementUI;
using ElementUI.Ugui;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// A small switch that only shows a state: on, off, or halfway for a choice
/// that mixes both, faded while there is nothing for it to switch. The
/// button around it takes the click, as the editor bar's Animate does. Its
/// thumb slides unless motion is reduced.
/// </summary>
internal sealed class SwitchIndicator : NativeView
{
  private const float Width = 30,
    Height = 18,
    Inset = 4,
    Thumb = Height - Inset * 2;

  private static readonly Color Off = new(.3f, .3f, .3f);

  private readonly Expression<Func<bool?>> _on;
  private readonly Expression<Func<bool>> _available;
  private UiCapsuleGraphic _track,
    _thumb;
  private UiSwitchMotion _motion;
  private bool? _shownOn;
  private bool _shownAvailable = true;

  /// <param name="on">On, off, or null for halfway.</param>
  /// <param name="available">False fades it: there is nothing for it to switch.</param>
  public SwitchIndicator(Expression<Func<bool?>> on, Expression<Func<bool>> available)
  {
    _on = on;
    _available = available;
  }

  protected override void Build(RectTransform rect)
  {
    _track = RectPeer.Child(rect, "Switch").gameObject.AddComponent<UiCapsuleGraphic>();
    _track.raycastTarget = false;
    RectTransform thumb = RectPeer.Child(_track.rectTransform, "Thumb");
    thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0, 1);
    thumb.sizeDelta = new Vector2(Thumb, Thumb);
    _thumb = thumb.gameObject.AddComponent<UiCapsuleGraphic>();
    _thumb.raycastTarget = false;
    _motion = thumb.gameObject.AddComponent<UiSwitchMotion>();
    Watch(
      _on,
      on =>
      {
        _shownOn = on;
        Paint();
      }
    );
    Watch(
      _available,
      available =>
      {
        _shownAvailable = available;
        Paint();
      }
    );
  }

  private void Paint()
  {
    float alpha = _shownAvailable ? 1 : .4f;
    Color track = _shownOn == false ? Off : UiPalette.AccentSurface,
      thumb = _shownOn == false ? UiPalette.Foreground : UiPalette.SoftAccent;
    track.a *= alpha;
    thumb.a *= alpha;
    _track.color = track;
    _thumb.color = thumb;
    float travel = Width - Thumb - Inset * 2;
    float x =
      _shownOn == null ? travel * .5f
      : _shownOn.Value ? travel
      : 0;
    _motion.Set(Inset + x, UguiTheme.MotionSeconds <= 0);
  }

  protected override Size PreferredSize(Proposal proposal) => new(Width, Height);
}
