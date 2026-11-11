using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.Canvas;

/// <summary>Reports when its control is hovered or focused, for the status line's hint.</summary>
internal sealed class LayoutActionHint
  : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler
{
  public Action<bool> Changed;

  public void OnPointerEnter(PointerEventData data) => Changed?.Invoke(true);

  public void OnPointerExit(PointerEventData data) => Changed?.Invoke(false);

  public void OnSelect(BaseEventData data) => Changed?.Invoke(true);

  public void OnDeselect(BaseEventData data) => Changed?.Invoke(false);

  private void OnDisable() => Changed?.Invoke(false);
}
