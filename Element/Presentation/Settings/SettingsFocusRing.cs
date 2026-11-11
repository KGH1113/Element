using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Settings;

internal sealed class SettingsFocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
{
  private Outline _outline;

  public void Initialize(Outline outline) => _outline = outline;

  public void OnSelect(BaseEventData eventData)
  {
    if (_outline != null)
      _outline.enabled = eventData is not PointerEventData && !SettingsButton.PointerSelectedThisFrame;
  }

  public void OnDeselect(BaseEventData eventData)
  {
    if (_outline != null)
      _outline.enabled = false;
  }

  private void OnDisable()
  {
    if (_outline != null)
      _outline.enabled = false;
  }
}
