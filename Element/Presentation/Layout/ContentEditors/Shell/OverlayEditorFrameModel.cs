using System;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// What an editor's top line shows and does: its shortcuts, or for a few
/// seconds a notice in their place (warm for a warning), then Cancel and Done.
/// </summary>
internal sealed class OverlayEditorFrameModel : ViewModel
{
  private readonly string _hint;
  private bool _noticing;
  private float _noticeUntil;

  /// <param name="name">The editor's name; its buttons are named after it ("Text editor done").</param>
  /// <param name="hint">The shortcuts the line shows between notices.</param>
  public OverlayEditorFrameModel(string name, string hint, Action cancel, Action done)
  {
    Name = name;
    _hint = hint;
    Line = new State<string>(hint);
    Cancel = Command(cancel);
    Done = Command(done);
  }

  public readonly string Name;

  /// <summary>The shortcuts, or a notice.</summary>
  public readonly State<string> Line;

  /// <summary>Muted for the shortcuts, white for a note, warm for a warning.</summary>
  public readonly State<UiColor> Ink = new(Colors.Muted);

  /// <summary>Leaves without saving.</summary>
  public Command Cancel { get; }

  /// <summary>Saves and leaves, unless what is open can't be used yet.</summary>
  public Command Done { get; }

  /// <summary>The notice showing; empty once the shortcuts are back.</summary>
  public string NoticeText => _noticing ? Line.Value : "";

  /// <summary>A short message where the shortcuts are; a warning reads in a warm color.</summary>
  public void Notice(string message, bool warning)
  {
    Line.Value = message;
    Ink.Value = warning ? Colors.Danger : Colors.Foreground;
    _noticing = true;
    _noticeUntil = Time.unscaledTime + OverlayEditorFrame.NoticeSeconds;
  }

  /// <summary>Once a frame: the shortcuts come back once a notice has been read.</summary>
  public void Tick()
  {
    if (!_noticing || Time.unscaledTime <= _noticeUntil)
      return;
    _noticing = false;
    Line.Value = _hint;
    Ink.Value = Colors.Muted;
  }
}
