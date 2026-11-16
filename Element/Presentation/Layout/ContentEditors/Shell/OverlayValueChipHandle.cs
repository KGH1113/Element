using Element.Domain.Overlay.Values;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Hands a dock chip's drag to the dock. Taking the drag also keeps the values
/// from scrolling while one is carried out, and stops the press from
/// counting as a click.
/// </summary>
internal sealed class OverlayValueChipHandle
  : MonoBehaviour,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
  public OverlayValueDefinition Value;
  public OverlayInputDockModel Dock;

  /// <summary>Makes the chip drawn as <paramref name="target"/> carry <paramref name="value"/> out of <paramref name="dock"/>.</summary>
  public static void Attach(GameObject target, OverlayValueDefinition value, OverlayInputDockModel dock)
  {
    OverlayValueChipHandle handle = target.GetComponent<OverlayValueChipHandle>();
    if (handle == null)
      handle = target.AddComponent<OverlayValueChipHandle>();
    handle.Value = value;
    handle.Dock = dock;
  }

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
