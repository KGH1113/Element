using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Widgets;

/// <summary>An invisible area that reports presses, drags and hovering, for handles and the color bar.</summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class UiPointerArea
  : MaskableGraphic,
    IPointerDownHandler,
    IPointerUpHandler,
    IInitializePotentialDragHandler,
    IDragHandler,
    IPointerMoveHandler,
    IPointerExitHandler
{
  public Action<PointerEventData> Down,
    Dragged,
    Up,
    Moved;
  public Action Exited;

  public void OnPointerMove(PointerEventData data) => Moved?.Invoke(data);

  public void OnPointerExit(PointerEventData data) => Exited?.Invoke();

  protected override void OnPopulateMesh(VertexHelper mesh) => mesh.Clear();

  public void OnPointerDown(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Down?.Invoke(data);
  }

  // Handles follow the pointer from the first pixel.
  public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = false;

  public void OnDrag(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Dragged?.Invoke(data);
  }

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Up?.Invoke(data);
  }
}
