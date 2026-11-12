using System;
using Element.Presentation.Ui.Declarative;
using ElementKit;
using ElementUI;
using UnityEngine;
using static ElementUI.UI;

namespace Element.Presentation.Layout.ContentEditors.Mods;

/// <summary>
/// A mod's editing screen written with ElementUI
/// (<see cref="OverlayElement{TSettings}.Editor"/>): its view in a column
/// under the preview, scrolling when taller than the room. Its controls
/// change the settings themselves; each edit is handed to the editor
/// (<paramref name="edited"/>), and a drag (<paramref name="dragging"/>) is
/// one undo step.
/// </summary>
internal sealed class ModEditorView : IModEditorScreen
{
  private readonly ScreenPanel _panel;
  private readonly ModEditorContext _context;

  /// <param name="frame">The editor's controls, in frame units.</param>
  /// <param name="area">The room under the preview.</param>
  /// <param name="view">What the mod's <c>Editor</c> returned.</param>
  /// <param name="model">The view model it reads; it goes with the screen.</param>
  public ModEditorView(
    RectTransform frame,
    Rect area,
    View view,
    ViewModel model,
    IBackend backend,
    ModEditorContext context,
    Action edited,
    Action<bool> dragging
  )
  {
    _context = context;
    _panel = new ScreenPanel("Mod editor view", frame, Color.clear, keyboard: false);
    Root.anchoredPosition = new Vector2(area.x, -area.y);
    _panel.Mount(
      backend,
      ScrollView(
          view.Frame(width: ModEditorPage.ColumnWidth, alignment: Alignment.TopLeading)
            .Frame(maxWidth: float.PositiveInfinity, alignment: Alignment.Top)
        )
        .Frame(width: area.width, height: area.height),
      model,
      area.width
    );
    _panel.Host.Root.Edited += edited;
    _panel.Host.Root.Editing += dragging;
  }

  public RectTransform Root => _panel.Root;

  public bool Typing => _context.Recording || ModKeyboard.FocusedField(Root) != null;

  public bool HasPopover => _panel.Host != null && _panel.Host.Root != null && _panel.Host.Root.HasOverlay;

  public bool CloseTopPopover() => _panel.Host != null && _panel.Host.HandleEscape();

  public void Tick() => _panel.Sync();

  /// <summary>Undo put earlier settings back: what reads them shows them.</summary>
  public void Restored()
  {
    Runtime.Probe();
    _context.Restored();
  }

  public void Dispose() => _panel.Dispose();
}
