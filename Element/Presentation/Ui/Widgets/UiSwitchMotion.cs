using ElementUI;
using UnityEngine;
using Motion = ElementUI.Motion;

namespace Element.Presentation.Ui.Widgets;

internal sealed class UiSwitchMotion : MonoBehaviour
{
  private AnimatedFloat _position;

  public void Set(float target, bool immediate)
  {
    _position ??= new AnimatedFloat(x => ((RectTransform)transform).anchoredPosition = new Vector2(x, -4));
    _position.Set(target, immediate ? null : Motion.Exponential(24, .05f));
  }

  private void OnDisable() => _position?.Finish();

  private void OnDestroy() => _position?.Dispose();
}
