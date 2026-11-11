using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Settings;

internal sealed class SettingsGestureEnd : MonoBehaviour, IPointerUpHandler
{
  public Action Finished;

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Finished?.Invoke();
  }
}
