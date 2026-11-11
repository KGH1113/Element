using UnityEngine;

namespace Element.Presentation.Settings;

internal sealed class SettingsSwitchMotion : MonoBehaviour
{
  private float _target = float.NaN;

  public void Set(float target, bool immediate)
  {
    if (_target == target && !immediate)
      return;
    RectTransform rect = (RectTransform)transform;
    bool first = float.IsNaN(_target);
    _target = target;
    if (immediate || first)
      rect.anchoredPosition = new Vector2(target, -4);
    enabled = !immediate && !first;
  }

  private void Update()
  {
    RectTransform rect = (RectTransform)transform;
    float x = Mathf.Lerp(rect.anchoredPosition.x, _target, 1 - Mathf.Exp(-24 * Time.unscaledDeltaTime));
    if (Mathf.Abs(x - _target) < .05f)
    {
      x = _target;
      enabled = false;
    }
    rect.anchoredPosition = new Vector2(x, -4);
  }
}
