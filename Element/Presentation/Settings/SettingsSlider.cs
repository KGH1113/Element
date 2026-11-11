using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed class SettingsSlider : Slider
{
  public float Step = 1f / 255f;

  public override void OnMove(AxisEventData eventData)
  {
    if (!IsActive() || !IsInteractable())
      return;
    float delta = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Down ? -Step : Step;
    value += delta * (SettingsModifierKeys.Shift ? 10 : 1);
    eventData.Use();
  }
}
