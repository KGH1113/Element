using System;

namespace Element.Presentation.Layout.Canvas;

/// <summary>What the main toolbar's controls and the canvas's keys do; the editor fills them in.</summary>
internal sealed class LayoutCommands
{
  public LayoutCommands(
    Action undo,
    Action redo,
    Action cancel,
    Action save,
    Action resetAll,
    Action resetSelected,
    Action delete,
    Action add,
    Action color,
    Action font,
    Action edit
  )
  {
    Undo = undo;
    Redo = redo;
    Cancel = cancel;
    Save = save;
    ResetAll = resetAll;
    ResetSelected = resetSelected;
    Delete = delete;
    Add = add;
    Color = color;
    Font = font;
    Edit = edit;
  }

  public Action Undo { get; }
  public Action Redo { get; }
  public Action Cancel { get; }
  public Action Save { get; }
  public Action ResetAll { get; }
  public Action ResetSelected { get; }
  public Action Delete { get; }
  public Action Add { get; }
  public Action Color { get; }
  public Action Font { get; }

  /// <summary>Opens the selected element's editor.</summary>
  public Action Edit { get; }
}
