using Element.Domain.Overlay.Values;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.OverlayEditor.Parts.Dock.ValueBrowser;

/// <summary>
/// A chip's drag, handed to the values' view model as a value carried out of
/// the dock. Taking the drag also keeps the values from scrolling while one
/// is carried out, and stops the press from counting as a click.
/// </summary>
internal sealed class ValueChipDragInput
  : MonoBehaviour,
    IInitializePotentialDragHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
  public OverlayValueDefinition Value;
  public ValueBrowserViewModel Values;

  /// <summary>Makes the chip drawn as <paramref name="target"/> carry <paramref name="value"/> out of the dock.</summary>
  public static void Attach(GameObject target, OverlayValueDefinition value, ValueBrowserViewModel values)
  {
    ValueChipDragInput gesture = target.GetComponent<ValueChipDragInput>();
    if (gesture == null)
      gesture = target.AddComponent<ValueChipDragInput>();
    gesture.Value = value;
    gesture.Values = values;
  }

  public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;

  public void OnBeginDrag(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Values?.BeginCarry(Value, data.position);
  }

  public void OnDrag(PointerEventData data) => Values?.Carry(data.position);

  public void OnEndDrag(PointerEventData data) => Values?.EndCarry(data.position);

  // A dock closed mid-drag drops nothing.
  private void OnDisable() => Values?.CancelCarry();
}
