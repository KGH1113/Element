using System;
using UnityEngine;

namespace Element.Presentation.Settings;

/// <summary>Fades the notice in with a short drop, holds it, then fades it out and hides it.</summary>
internal sealed class SettingsToastMotion : MonoBehaviour
{
  private const float Enter = .18f;
  private const float Hold = 1.8f;
  private const float Exit = .22f;
  private const float Drop = 8;
  private CanvasGroup _group;
  private RectTransform _rect;
  private Action _hidden;
  private float _elapsed;
  private bool _reduceMotion;

  public void Initialize(CanvasGroup group, Action hidden)
  {
    _group = group;
    _rect = (RectTransform)transform;
    _hidden = hidden;
    enabled = false;
  }

  /// <summary>Starts over, so pressing shortcuts in a row keeps the latest notice up.</summary>
  public void Play(bool reduceMotion)
  {
    _reduceMotion = reduceMotion;
    _elapsed = 0;
    enabled = true;
    Apply();
  }

  private void Update()
  {
    _elapsed += Time.unscaledDeltaTime;
    if (_elapsed >= Enter + Hold + Exit)
    {
      enabled = false;
      _hidden();
      return;
    }
    Apply();
  }

  private void Apply()
  {
    if (_reduceMotion)
    {
      _group.alpha = 1;
      _rect.anchoredPosition = new Vector2(0, -24);
      return;
    }
    float enter = Mathf.Clamp01(_elapsed / Enter);
    float exit = Mathf.Clamp01((_elapsed - Enter - Hold) / Exit);
    float eased = 1 - (1 - enter) * (1 - enter) * (1 - enter);
    _group.alpha = eased * (1 - exit);
    _rect.anchoredPosition = new Vector2(0, -24 + Drop * (1 - eased));
  }
}
