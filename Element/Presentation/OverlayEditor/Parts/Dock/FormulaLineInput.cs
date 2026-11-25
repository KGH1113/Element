using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.OverlayEditor.Parts.Dock;

/// <summary>
/// A click on the dock's line: the piece under the pointer (a link in its
/// text) goes up by its index, or -1 for none.
/// </summary>
internal sealed class FormulaLineInput : MonoBehaviour, IPointerClickHandler
{
  private TextMeshProUGUI _text;
  private Action<int> _clicked;

  public void Initialize(TextMeshProUGUI text, Action<int> clicked)
  {
    _text = text;
    _clicked = clicked;
  }

  public void OnPointerClick(PointerEventData eventData)
  {
    if (_text == null || _clicked == null || eventData.button != PointerEventData.InputButton.Left)
      return;
    int link = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.pressEventCamera);
    if (link < 0 || link >= _text.textInfo.linkCount)
      return;
    if (int.TryParse(_text.textInfo.linkInfo[link].GetLinkID(), out int index))
      _clicked(index);
  }
}
