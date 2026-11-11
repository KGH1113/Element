using UnityEngine;
using UnityEngine.EventSystems;
using Element.Domain.Overlay.Values;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Hands a chip's drag to its dock. Taking the drag also keeps the values from
/// scrolling while one is carried out.
/// </summary>
internal sealed class OverlayValueChipHandle
  : MonoBehaviour,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
  public OverlayValueDefinition Value;
  public OverlayInputDock Dock;

  public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;

  public void OnBeginDrag(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Dock?.BeginDrag(Value, data.position);
  }

  public void OnDrag(PointerEventData data) => Dock?.Drag(data.position);

  public void OnEndDrag(PointerEventData data) => Dock?.EndDrag(data.position);

  // A dock closed mid-drag drops nothing.
  private void OnDisable() => Dock?.CancelDrag();
}
