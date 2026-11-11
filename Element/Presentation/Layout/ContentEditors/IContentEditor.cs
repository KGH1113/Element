using System;
using Element.Application.Layout;

namespace Element.Presentation.Layout.ContentEditors;

/// <summary>
/// A full-screen editor for one element's content: the text editor, the
/// shape editor and the editor of other mods' elements. While one is open
/// the layout editor only ticks it.
/// </summary>
internal interface IContentEditor : IDisposable
{
  bool IsOpen { get; }

  /// <summary>
  /// Opens <paramref name="target"/>; <paramref name="closed"/> hears whether
  /// it was finished (true) or cancelled. False when this editor can't edit it now.
  /// </summary>
  bool Open(IOverlayLayoutTarget target, Action<bool> closed);

  /// <summary>Keeps it above the layout toolbar and handles, which raise themselves.</summary>
  void Raise();

  /// <summary>Once a frame while open: typing, the mouse and its popovers.</summary>
  void Tick();

  /// <summary>Esc: closes what is open in it, or with nothing open finishes like Done.</summary>
  void Escape();

  /// <summary>
  /// Done (<paramref name="commit"/>) keeps the edit, Cancel throws it away;
  /// with <paramref name="notify"/> the opener hears it. It stays open when
  /// what was typed can't be used yet.
  /// </summary>
  void Close(bool commit, bool notify);
}
