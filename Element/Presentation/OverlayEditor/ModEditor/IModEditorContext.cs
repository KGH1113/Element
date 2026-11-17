using ElementUI;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>What a mod's <see cref="ModEditorPage"/> reads from the editor around it.</summary>
internal interface IModEditorContext
{
  /// <summary>The tab last picked in the tabs named <paramref name="key"/>, so undo keeps it.</summary>
  int TabOf(string key);

  /// <summary>What Element's pickers on the page need: fonts, and a turn every frame.</summary>
  ModPickerContext Pickers { get; }

  /// <summary>Draws the page in the editor's look.</summary>
  IBackend NewBackend();
}
