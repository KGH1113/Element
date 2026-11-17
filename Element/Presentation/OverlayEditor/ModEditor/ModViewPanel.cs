using System;
using ElementKit;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// A mod's editing screen written with ElementUI
/// (<see cref="OverlayElement{TSettings}.Editor"/>): its view in the
/// property column, its controls drawn as the column's rows
/// (<see cref="ModColumnStyle"/>) without the mod doing anything for it.
/// Its controls change the settings themselves; each edit is handed to the
/// editor (<paramref name="edited"/>), and a drag (<paramref name="dragging"/>)
/// is one undo step.
/// </summary>
internal sealed class ModViewPanel : IModColumnPanel
{
  private readonly ModEditorColumn _column;
  private readonly ModEditorContext _context;

  /// <param name="frame">The editor's controls, in frame units.</param>
  /// <param name="area">Where the column stands.</param>
  /// <param name="view">What the mod's <c>Editor</c> returned.</param>
  /// <param name="model">The view model it reads; it goes with the screen.</param>
  /// <param name="measure">How wide words are at the rows' size.</param>
  public ModViewPanel(
    RectTransform frame,
    Rect area,
    View view,
    ViewModel model,
    Func<IBackend> backend,
    ModEditorContext context,
    Func<string, float> measure,
    Action edited,
    Action<bool> dragging
  )
  {
    _context = context;
    _column = new ModEditorColumn(frame, area, backend, edited, dragging);
    context.Column = _column;
    _column.Mount(view, new ModColumnStyle(_column, measure), model);
  }

  public ModEditorColumn Column => _column;

  public RectTransform Root => _column.Root;

  public bool Typing => _context.Recording || _column.Typing;

  public bool HasPopover => _column.HasPopover;

  public bool CloseTopPopover() => _column.CloseTopPopover();

  public void Tick() => _column.Tick();

  /// <summary>Undo put earlier settings back: what reads them shows them.</summary>
  public void Restored()
  {
    Runtime.Probe();
    _context.Restored();
  }

  public void Dispose()
  {
    if (_context.Column == _column)
      _context.Column = null;
    _column.Dispose();
  }
}
