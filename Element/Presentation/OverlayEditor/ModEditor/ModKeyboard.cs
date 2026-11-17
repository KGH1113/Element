using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>Who has the keyboard on a mod's editing screen.</summary>
internal static class ModKeyboard
{
  /// <summary>The text field under <paramref name="root"/> being typed in; null when none is.</summary>
  public static GameObject FocusedField(Transform root)
  {
    GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
    if (selected == null || root == null || !selected.transform.IsChildOf(root))
      return null;
    return selected.TryGetComponent(out TMP_InputField field) && field.isFocused ? selected : null;
  }
}
