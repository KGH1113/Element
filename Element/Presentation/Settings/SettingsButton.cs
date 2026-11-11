using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

// Theme changes must not start a new hover tween on every slider event. Ordinary
// pointer/focus transitions still use Button's normal animation between states.
internal sealed class SettingsButton : Button
{
  private bool _hovered;
  private CanvasGroup _disabledGroup;
  private static int _pointerSelectionFrame = -1;
  internal static bool PointerSelectedThisFrame => _pointerSelectionFrame == Time.frameCount;

  protected override void Awake()
  {
    base.Awake();
    _disabledGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
  }

  protected override void OnEnable()
  {
    _hovered = false;
    base.OnEnable();
  }

  protected override void OnDisable()
  {
    _hovered = false;
    base.OnDisable();
  }

  public override void OnPointerEnter(PointerEventData data)
  {
    _hovered = true;
    base.OnPointerEnter(data);
  }

  public override void OnPointerExit(PointerEventData data)
  {
    _hovered = false;
    base.OnPointerExit(data);
  }

  public override void OnPointerDown(PointerEventData data)
  {
    if (data.button == PointerEventData.InputButton.Left)
      _pointerSelectionFrame = Time.frameCount;
    base.OnPointerDown(data);
  }

  public override void OnPointerClick(PointerEventData data)
  {
    // Navigation callbacks select the next page's first control without
    // forwarding PointerEventData. Preserve the source of that selection.
    if (data.button == PointerEventData.InputButton.Left)
      _pointerSelectionFrame = Time.frameCount;
    base.OnPointerClick(data);
  }

  protected override void DoStateTransition(SelectionState state, bool instant)
  {
    // Unity keeps a clicked control selected after the pointer leaves it.
    // Reopened pages can also carry cached pointer state from before hiding.
    if (state == SelectionState.Selected || state == SelectionState.Highlighted)
      state = _hovered ? SelectionState.Highlighted : SelectionState.Normal;
    if (_disabledGroup == null)
      _disabledGroup = GetComponent<CanvasGroup>();
    if (_disabledGroup != null)
      _disabledGroup.alpha = state == SelectionState.Disabled ? .5f : 1;
    base.DoStateTransition(state, instant);
  }

  public void SetPalette(ColorBlock palette)
  {
    colors = palette;
    DoStateTransition(currentSelectionState, true);
  }
}
