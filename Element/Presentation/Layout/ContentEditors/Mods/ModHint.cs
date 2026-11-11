using System;
using System.Collections.Generic;
using ElementKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>Shows a part's hint while the pointer is over it.</summary>
internal sealed class ModHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
  public ModEditorPage Page;
  public RectTransform Anchor;
  public string Text;

  public void OnPointerEnter(PointerEventData eventData)
  {
    if (!string.IsNullOrEmpty(Text))
      Page?.ShowTip(Text, Anchor);
  }

  public void OnPointerExit(PointerEventData eventData) => Page?.HideTip();

  private void OnDisable() => Page?.HideTip();
}
