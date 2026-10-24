using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ImplResourcePack.Presentation.Settings;

internal sealed class SettingsColorPlane : Selectable, IDragHandler
{
  public Action<float, float> Changed;
  public Action Finished;
  public RectTransform Marker;
  public SettingsGradientGraphic Gradient;
  private float _saturation,
    _value;

  public void Set(float hue, float saturation, float value)
  {
    _saturation = saturation;
    _value = value;
    Gradient.SetHue(hue);
    Marker.anchorMin = Marker.anchorMax = new Vector2(saturation, value);
    Marker.anchoredPosition = Vector2.zero;
  }

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
    Changed?.Invoke(
      Mathf.Clamp01((p.x - rect.rect.xMin) / rect.rect.width),
      Mathf.Clamp01((p.y - rect.rect.yMin) / rect.rect.height)
    );
  }

  public override void OnMove(AxisEventData data)
  {
    float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? .1f : .01f;
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

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class SettingsGradientGraphic : MaskableGraphic
{
  public bool HueStrip;
  private float _hue = -1;

  public void SetHue(float hue)
  {
    if (_hue == hue)
      return;
    _hue = hue;
    SetVerticesDirty();
  }

  protected override void OnPopulateMesh(VertexHelper mesh)
  {
    mesh.Clear();
    Rect r = rectTransform.rect;
    int segments = HueStrip ? 6 : 1;
    for (int i = 0; i < segments; i++)
    {
      Color left = HueStrip ? Color.HSVToRGB(i / 6f, 1, 1) : Color.white;
      Color right = Color.HSVToRGB(HueStrip ? (i + 1) / 6f : Mathf.Max(0, _hue), 1, 1);
      float x0 = r.xMin + r.width * i / segments;
      float x1 = r.xMin + r.width * (i + 1) / segments;
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector3(x0, r.yMin), left, Vector2.zero);
      mesh.AddVert(new Vector3(x0, r.yMax), left, Vector2.zero);
      mesh.AddVert(new Vector3(x1, r.yMax), right, Vector2.zero);
      mesh.AddVert(new Vector3(x1, r.yMin), right, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
    if (!HueStrip)
    {
      // Multiply the horizontal white-to-hue ramp by a vertical value ramp.
      // A single four-corner quad interpolates across its diagonal and is not HSV.
      int start = mesh.currentVertCount;
      mesh.AddVert(new Vector3(r.xMin, r.yMin), Color.black, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMin, r.yMax), Color.clear, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMax, r.yMax), Color.clear, Vector2.zero);
      mesh.AddVert(new Vector3(r.xMax, r.yMin), Color.black, Vector2.zero);
      mesh.AddTriangle(start, start + 1, start + 2);
      mesh.AddTriangle(start, start + 2, start + 3);
    }
  }
}

internal sealed class SettingsGestureEnd : MonoBehaviour, IPointerUpHandler
{
  public Action Finished;

  public void OnPointerUp(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      Finished?.Invoke();
  }
}
