using System;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// What a mod's <see cref="ModEditorPage"/> asks of the editor around it, and
/// the only way up from its parts. What it reads goes the other way, through
/// <see cref="IModEditorContext"/>.
/// </summary>
internal interface IModEditorActions
{
  /// <summary>
  /// Runs a part's change as one edit: undo, then the mod's callback, then the
  /// preview; <paramref name="rebuild"/> makes the parts again to show it.
  /// </summary>
  void Change(object key, Action apply, bool rebuild);

  /// <summary>A tab was picked in the tabs named <paramref name="key"/>; undo keeps it.</summary>
  void SetTab(string key, int index);
}
