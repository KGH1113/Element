using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.Canvas;

/// <summary>While its field is focused, keys go to the field and not to the game.</summary>
internal sealed class LayoutTextCapture : MonoBehaviour, ISelectHandler, IDeselectHandler
{
  public void OnSelect(BaseEventData data) => UIInteractionState.SetTextEditing(true);

  public void OnDeselect(BaseEventData data) => UIInteractionState.SetTextEditing(false);

  private void OnDisable() => UIInteractionState.SetTextEditing(false);
}
