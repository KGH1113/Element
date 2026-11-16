using System;
using ElementUI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>What a mod's <see cref="ModEditorPage"/> asks of the editor around it.</summary>
internal interface IModEditorHost
{
  /// <summary>
  /// Runs a part's change as one edit: undo, then the mod's callback, then the
  /// preview; <paramref name="rebuild"/> makes the parts again to show it.
  /// </summary>
  void Change(object key, Action apply, bool rebuild);

  /// <summary>The tab last picked in the tabs named <paramref name="key"/>, so undo keeps it.</summary>
  int TabOf(string key);

  void SetTab(string key, int index);

  /// <summary>What Element's pickers on the page need: fonts, and a turn every frame.</summary>
  ModEditorContext Context { get; }

  /// <summary>Draws the page in the editor's look.</summary>
  IBackend NewBackend();
}
