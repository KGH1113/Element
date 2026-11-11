using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>Turns presses and drags on a range slider's track into the handle they move.</summary>
internal sealed class ModRangeDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
{
  /// <summary>The pointer at a fraction across the track; true when it just went down.</summary>
  public Action<float, bool> Moved;

  public RectTransform Track;

  public void OnPointerDown(PointerEventData eventData) => Report(eventData, true);

  public void OnDrag(PointerEventData eventData) => Report(eventData, false);

  private void Report(PointerEventData eventData, bool down)
  {
    if (
      Track == null
      || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
        Track,
        eventData.position,
        eventData.pressEventCamera,
        out Vector2 local
      )
    )
      return;
    Rect rect = Track.rect;
    float fraction = rect.width > 0 ? Mathf.Clamp01((local.x - rect.xMin) / rect.width) : 0;
    Moved?.Invoke(fraction, down);
  }
}
