using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// What a mod's element shows in the property column beside the preview:
/// the parts it made with <c>EditorPage</c> (<see cref="ModEditorPage"/>), or
/// a screen it wrote with ElementUI (<see cref="ModViewPanel"/>). The editor
/// around it asks the same of both.
/// </summary>
internal interface IModColumnPanel : IDisposable
{
  /// <summary>Where it is drawn, under the editor's controls.</summary>
  RectTransform Root { get; }

  /// <summary>The property column it stands in; the preview keeps left of it and of the tool open there.</summary>
  ModEditorColumn Column { get; }

  /// <summary>Whether the keyboard is its: typing in a field, or recording a shortcut.</summary>
  bool Typing { get; }

  /// <summary>Whether a tool of its rows is open in the column (a color picker, a list), or a list a control opened.</summary>
  bool HasPopover { get; }

  /// <summary>Closes its topmost popover; false when none was open.</summary>
  bool CloseTopPopover();

  /// <summary>Once a frame.</summary>
  void Tick();
}
