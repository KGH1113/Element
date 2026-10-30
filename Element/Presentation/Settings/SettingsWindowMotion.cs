using System;
using UnityEngine;

namespace Element.Presentation.Settings;

// A short, critically damped settle keeps the window at its final pixel scale.
// Scaling text/controls while fading them in makes the whole settings UI look
// soft and unsettled, especially at low game resolutions.
internal sealed class SettingsWindowMotion : MonoBehaviour
{
  private CanvasGroup _group;
  private RectTransform _rect;
  private Func<bool> _reduceMotion;
  private float _elapsed,
    _lastOffset;
  private Vector2 _appliedPosition;

  public void Initialize(CanvasGroup group, Func<bool> reduceMotion)
  {
    _group = group;
    _rect = (RectTransform)transform;
    _reduceMotion = reduceMotion;
    enabled = false;
  }

  public void Show()
  {
    if (_rect == null)
      return;
    RemoveOffset();
    _elapsed = 0;
    _group.alpha = _reduceMotion() ? 1 : 0;
    _rect.localScale = Vector3.one;
    enabled = !_reduceMotion();
    if (enabled)
      ApplyOffset(-8);
  }

  private void Update()
  {
    RemoveOffset();
    _elapsed += Time.unscaledDeltaTime;
    float phase = _elapsed * 32;
    float remaining = (1 + phase) * Mathf.Exp(-phase);
    _group.alpha = 1 - Mathf.Exp(-_elapsed * 24);
    if (_reduceMotion() || _elapsed >= .24f)
    {
      _group.alpha = 1;
      enabled = false;
      return;
    }
    ApplyOffset(-8 * remaining);
  }

  private void ApplyOffset(float offset)
  {
    _lastOffset = offset;
    _rect.anchoredPosition += new Vector2(0, offset);
    _appliedPosition = _rect.anchoredPosition;
  }

  private void RemoveOffset()
  {
    if (_rect == null || _lastOffset == 0)
      return;
    // FitWindow may have replaced the position during a resolution change.
    if (_rect.anchoredPosition == _appliedPosition)
      _rect.anchoredPosition -= new Vector2(0, _lastOffset);
    _lastOffset = 0;
  }

  private void OnDisable() => RemoveOffset();
}
