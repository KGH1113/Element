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
internal sealed class ModViewPanel : IDisposable
{
  private readonly ModEditorColumn _column;
  private readonly ModPickerContext _context;

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
    ModPickerContext context,
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

  /// <summary>The property column it stands in; the preview keeps left of it and of the tool open there.</summary>
  public ModEditorColumn Column => _column;

  /// <summary>Where it is drawn, under the editor's controls.</summary>
  public RectTransform Root => _column.Root;

  /// <summary>Whether the keyboard is its: typing in a field, or recording a shortcut.</summary>
  public bool Typing => _context.Recording || _column.Typing;

  /// <summary>Whether a tool of its rows is open in the column (a color picker, a list), or a list a control opened.</summary>
  public bool HasPopover => _column.HasPopover;

  /// <summary>Closes its topmost popover; false when none was open.</summary>
  public bool CloseTopPopover() => _column.CloseTopPopover();

  /// <summary>Once a frame.</summary>
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
