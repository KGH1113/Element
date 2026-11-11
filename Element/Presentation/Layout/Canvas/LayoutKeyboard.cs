using System.Collections.Generic;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Layout.Kinds;
using UnityEngine;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// The canvas's keys: Tab through elements, undo and redo, delete, select
/// all, Enter to edit, arrows to move and + or − to scale. While a level is
/// played they need Cmd/Ctrl, so rhythm keys keep reaching the game.
/// </summary>
internal sealed class LayoutKeyboard
{
  private readonly LayoutSession _session;
  private readonly LayoutSelection _selection;
  private readonly LayoutDrag _drag;
  private readonly LayoutToolbar _toolbar;
  private readonly ElementKinds _kinds;
  private readonly LayoutCommands _commands;

  public LayoutKeyboard(
    LayoutSession session,
    LayoutSelection selection,
    LayoutDrag drag,
    LayoutToolbar toolbar,
    ElementKinds kinds,
    LayoutCommands commands
  )
  {
    _session = session;
    _selection = selection;
    _drag = drag;
    _toolbar = toolbar;
    _kinds = kinds;
    _commands = commands;
  }

  public void Handle()
  {
    List<LayoutFrame> frames = _session.Frames;
    if (_drag.Active || _toolbar.Dragging || frames.Count == 0)
      return;
    bool gameplay = LayoutSession.GameplayActive;
    if (gameplay && !EditorKeys.CommandHeld)
      return;
    if (EditorKeys.Pressed(KeyCode.Tab) && !EditorKeys.AltHeld)
    {
      int direction = EditorKeys.ShiftHeld ? -1 : 1;
      for (int count = 0; count < frames.Count; count++)
      {
        _selection.Selected = (_selection.Selected + direction + frames.Count) % frames.Count;
        IOverlayLayoutTarget next = frames[_selection.Selected].Target;
        if (next.Available && next.Enabled)
          break;
      }
    }
    if (EditorKeys.CommandHeld && EditorKeys.Pressed(KeyCode.Z))
    {
      if (EditorKeys.ShiftHeld)
        _commands.Redo();
      else
        _commands.Undo();
      return;
    }
    if (EditorKeys.CommandHeld && EditorKeys.Pressed(KeyCode.Y))
    {
      _commands.Redo();
      return;
    }
    if (EditorKeys.CommandHeld && (EditorKeys.Pressed(KeyCode.Delete) || EditorKeys.Pressed(KeyCode.Backspace)))
    {
      _commands.Delete();
      return;
    }
    if (EditorKeys.CommandHeld && EditorKeys.Pressed(KeyCode.A))
    {
      _selection.SelectAll();
      return;
    }
    if (!_selection.HasSelection)
      return;
    // Enter edits one element; it has no meaning for several.
    if (!gameplay && !_selection.Multi && EditorKeys.EnterPressed && _kinds.Edit(frames[_selection.Selected].Target))
      return;
    float step = EditorKeys.ShiftHeld ? 10f : 1f;
    float dx =
      EditorKeys.Pressed(KeyCode.LeftArrow) ? -step
      : EditorKeys.Pressed(KeyCode.RightArrow) ? step
      : 0f;
    float dy =
      EditorKeys.Pressed(KeyCode.UpArrow) ? -step
      : EditorKeys.Pressed(KeyCode.DownArrow) ? step
      : 0f;
    if (dx != 0f || dy != 0f)
      _drag.Nudge(dx, dy);
    // Elements sized in their editor don't scale; + and − scale the rest, one at a time.
    bool scalable = !_selection.Multi && !_kinds.SizedInEditor(frames[_selection.Selected].Target);
    bool grow =
      scalable
      && (
        EditorKeys.Pressed(KeyCode.Equals) || EditorKeys.Pressed(KeyCode.Plus) || EditorKeys.Pressed(KeyCode.KeypadPlus)
      );
    bool shrink = scalable && (EditorKeys.Pressed(KeyCode.Minus) || EditorKeys.Pressed(KeyCode.KeypadMinus));
    if (grow || shrink)
    {
      float ratio = grow ? 1.02f : 1f / 1.02f;
      if (step > 1f)
        ratio = grow ? 1.1f : 1f / 1.1f;
      _toolbar.ScaleBy(ratio);
    }
    if (dx != 0 || dy != 0 || grow || shrink)
      _session.Commit();
  }
}
