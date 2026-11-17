using System;
using System.Collections.Generic;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Ui.Declarative.Controls;

namespace Element.Presentation.OverlayEditor.ModEditor;

/// <summary>
/// What Element's pickers on a mod's editing screen need from the editor
/// around them: the overlay fonts, reduced motion, the column their tools
/// open in, and a turn every frame for a shortcut field recording keys and
/// the font list scrolling. Both kinds of
/// screen use it: one built from <c>EditorPage</c>'s parts and one a mod
/// writes with ElementUI.
/// </summary>
internal sealed class ModEditorContext
{
  private readonly Func<OverlayFonts> _fonts;
  private readonly List<ShortcutFieldViewModel> _shortcuts = new();
  private readonly List<Action> _restored = new();
  private Action _fontTick;

  public ModEditorContext(Func<OverlayFonts> fonts, bool reduceMotion)
  {
    _fonts = fonts;
    ReduceMotion = reduceMotion;
  }

  public OverlayFonts Fonts => _fonts?.Invoke();

  public bool ReduceMotion { get; }

  /// <summary>The property column the screen stands in: its rows open their tools there.</summary>
  public ModEditorColumn Column { get; set; }

  /// <summary>Whether a shortcut field is recording: the keyboard is its until it ends.</summary>
  public bool Recording
  {
    get
    {
      foreach (ShortcutFieldViewModel shortcut in _shortcuts)
        if (shortcut.Recording)
          return true;
      return false;
    }
  }

  /// <summary>
  /// What a picker calls after it changed a value outside a control's own
  /// edit (a font picked from the list, a key recorded), so the screen
  /// records it as it does any edit.
  /// </summary>
  public Action Edits { private get; set; }

  public void Edited() => Edits?.Invoke();

  /// <summary>A shortcut field on the screen; it records keys in <see cref="Tick"/>.</summary>
  public void Track(ShortcutFieldViewModel shortcut) => _shortcuts.Add(shortcut);

  /// <summary>Takes the open font list's per-frame work, and null when it closes.</summary>
  public void FontTicks(Action tick) => _fontTick = tick;

  /// <summary>
  /// Runs <paramref name="refresh"/> after undo puts earlier settings back:
  /// for a picker that shows a value it was told rather than one it watches.
  /// </summary>
  public void OnRestored(Action refresh) => _restored.Add(refresh);

  public void Restored()
  {
    foreach (Action refresh in _restored)
      refresh();
  }

  /// <summary>Once a frame: the open font list, and keys for a recording shortcut field.</summary>
  public void Tick()
  {
    _fontTick?.Invoke();
    foreach (ShortcutFieldViewModel shortcut in _shortcuts)
      shortcut.TickCapture();
  }

  /// <summary>Stops recording without changing anything; Esc does the same.</summary>
  public void CancelRecording()
  {
    foreach (ShortcutFieldViewModel shortcut in _shortcuts)
      shortcut.Cancel();
  }

  /// <summary>The screen was made again: what its old parts tracked goes.</summary>
  public void Clear()
  {
    _shortcuts.Clear();
    _restored.Clear();
    _fontTick = null;
  }
}
