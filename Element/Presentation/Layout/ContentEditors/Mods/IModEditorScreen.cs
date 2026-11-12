using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// What a mod's element shows under the preview: the parts it made with
/// <c>EditorPage</c> (<see cref="ModEditorPage"/>), or a screen it wrote with
/// ElementUI (<see cref="ModEditorView"/>). The editor around it asks the same of both.
/// </summary>
internal interface IModEditorScreen : IDisposable
{
  /// <summary>Where it is drawn, under the editor's controls.</summary>
  RectTransform Root { get; }

  /// <summary>Whether the keyboard is its: typing in a field, or recording a shortcut.</summary>
  bool Typing { get; }

  /// <summary>Whether a popover of its parts is open: a color picker, a list.</summary>
  bool HasPopover { get; }

  /// <summary>Closes its topmost popover; false when none was open.</summary>
  bool CloseTopPopover();

  /// <summary>Once a frame.</summary>
  void Tick();
}

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
