using System;
using ElementUI;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// What an editor's header shows and does: what is edited, Cancel and Done,
/// and for a few seconds a notice (warm for a warning). An editor with an
/// input dock shows its notices on the dock's line instead (<see cref="InDock"/>).
/// </summary>
internal sealed class OverlayEditorFrameModel : ViewModel
{
  private float _noticeUntil;

  /// <param name="name">The editor's name; its buttons are named after it ("Text editor done").</param>
  /// <param name="title">What is edited, as the header says it ("Edit text").</param>
  public OverlayEditorFrameModel(string name, string title, Action cancel, Action done)
  {
    Name = name;
    Title = title;
    Cancel = Command(cancel);
    Done = Command(done);
  }

  public readonly string Name;

  public readonly string Title;

  /// <summary>The notice showing, wherever it shows; empty for none.</summary>
  public readonly State<string> Line = new("");

  /// <summary>A notice is showing.</summary>
  public readonly State<bool> Noticing = new(false);

  /// <summary>White for a note, warm for a warning.</summary>
  public readonly State<UiColor> Ink = new(Colors.Foreground);

  /// <summary>The notice in the header: empty while a dock shows it.</summary>
  public readonly State<string> HeaderNotice = new("");

  /// <summary>An input dock shows the notices on its line.</summary>
  public bool InDock { get; set; }

  /// <summary>Leaves without saving.</summary>
  public Command Cancel { get; }

  /// <summary>Saves and leaves, unless what is open can't be used yet.</summary>
  public Command Done { get; }

  /// <summary>The notice showing; empty once it has been read.</summary>
  public string NoticeText => Noticing.Value ? Line.Value : "";

  /// <summary>A short message for a few seconds; a warning reads in a warm color.</summary>
  public void Notice(string message, bool warning)
  {
    Line.Value = message ?? "";
    Ink.Value = warning ? Colors.Danger : Colors.Foreground;
    Noticing.Value = true;
    HeaderNotice.Value = InDock ? "" : Line.Value;
    _noticeUntil = Time.unscaledTime + OverlayEditorFrame.NoticeSeconds;
  }

  /// <summary>Once a frame: a notice goes once it has been read.</summary>
  public void Tick()
  {
    if (!Noticing.Value || Time.unscaledTime <= _noticeUntil)
      return;
    Noticing.Value = false;
    Line.Value = "";
    HeaderNotice.Value = "";
  }
}
