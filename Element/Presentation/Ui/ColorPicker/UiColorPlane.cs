using System;
using Element.Presentation.Ui.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Ui.ColorPicker;

internal sealed class UiColorPlane : Selectable, IDragHandler
{
  public Action<float, float> Changed;
  public Action Finished;
  public RectTransform Marker;
  public UiGradientGraphic Gradient;
  private float _saturation,
    _value;

  public void Set(float hue, float saturation, float value)
  {
    _saturation = saturation;
    _value = value;
    Gradient.SetHue(hue);
    Marker.anchorMin = Marker.anchorMax = new Vector2(saturation, value);
    // The ring stops at the square's edges, whole, as the bars' handles do.
    float half = MarkerHalf;
    Marker.anchoredPosition = new Vector2(Mathf.Lerp(half, -half, saturation), Mathf.Lerp(half, -half, value));
  }

  private float MarkerHalf => Marker.rect.width * .5f;

  public override void OnPointerDown(PointerEventData data)
  {
    if (!IsActive() || !IsInteractable() || data.button != PointerEventData.InputButton.Left)
      return;
    base.OnPointerDown(data);
    EventSystem.current?.SetSelectedGameObject(gameObject);
    Pick(data);
  }

  public override void OnPointerUp(PointerEventData data)
  {
    base.OnPointerUp(data);
    if (data.button == PointerEventData.InputButton.Left)
      Finished?.Invoke();
  }

  public void OnDrag(PointerEventData data)
  {
    if (IsActive() && IsInteractable() && data.button == PointerEventData.InputButton.Left)
      Pick(data);
  }

  private void Pick(PointerEventData data)
  {
    RectTransform rect = (RectTransform)transform;
    if (
      !RectTransformUtility.ScreenPointToLocalPointInRectangle(
        rect,
        data.position,
        data.pressEventCamera,
        out Vector2 p
      )
    )
      return;
    // The ring's center travels inside the square by its own half, and follows the pointer.
    float half = MarkerHalf;
    Changed?.Invoke(
      Mathf.Clamp01((p.x - rect.rect.xMin - half) / Mathf.Max(1, rect.rect.width - half * 2)),
      Mathf.Clamp01((p.y - rect.rect.yMin - half) / Mathf.Max(1, rect.rect.height - half * 2))
    );
  }

  public override void OnMove(AxisEventData data)
  {
    float step = UiModifierKeys.Shift ? .1f : .01f;
    float s =
      _saturation
      + (
        data.moveDir == MoveDirection.Left ? -step
        : data.moveDir == MoveDirection.Right ? step
        : 0
      );
    float v =
      _value
      + (
        data.moveDir == MoveDirection.Down ? -step
        : data.moveDir == MoveDirection.Up ? step
        : 0
      );
    Changed?.Invoke(Mathf.Clamp01(s), Mathf.Clamp01(v));
    Finished?.Invoke();
    data.Use();
  }
}
