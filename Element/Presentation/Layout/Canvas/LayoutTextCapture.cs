using Element.Application.Input;
using Element.Presentation.Settings;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.Canvas;

/// <summary>While its field is focused, keys go to the field and not to the game.</summary>
internal sealed class LayoutTextCapture : MonoBehaviour, ISelectHandler, IDeselectHandler
{
  public void OnSelect(BaseEventData data) => InputGate.SetTextEditing(true);

  public void OnDeselect(BaseEventData data) => InputGate.SetTextEditing(false);

  private void OnDisable() => InputGate.SetTextEditing(false);
}
