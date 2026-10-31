using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Shared;

internal static class OverlayTextFactory
{
  public static GameObject CreateRect(
    string name,
    Transform parent,
    Vector2 anchor,
    Vector2 pivot,
    Vector2 anchoredPosition,
    Vector2 size
  )
  {
    GameObject gameObject = new(name, typeof(RectTransform));
    RectTransform transform = gameObject.GetComponent<RectTransform>();
    transform.SetParent(parent, false);
    transform.anchorMin = anchor;
    transform.anchorMax = anchor;
    transform.pivot = pivot;
    transform.anchoredPosition = anchoredPosition;
    transform.sizeDelta = size;
    return gameObject;
  }

  public static TextMeshProUGUI AddText(
    GameObject gameObject,
    TMP_FontAsset font,
    Material material,
    float fontSize,
    TextAlignmentOptions alignment
  )
  {
    TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
    text.font = font;
    text.fontSharedMaterial = material;
    text.fontSize = fontSize;
    text.alignment = alignment;
    text.raycastTarget = false;
    text.richText = true;
    return text;
  }
}
