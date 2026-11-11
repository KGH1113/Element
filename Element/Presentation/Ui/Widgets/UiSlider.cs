using Element.Presentation.Ui.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Widgets;

internal sealed class UiSlider : Slider
{
  public float Step = 1f / 255f;

  public override void OnMove(AxisEventData eventData)
  {
    if (!IsActive() || !IsInteractable())
      return;
    float delta = eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Down ? -Step : Step;
    value += delta * (UiModifierKeys.Shift ? 10 : 1);
    eventData.Use();
  }
}
