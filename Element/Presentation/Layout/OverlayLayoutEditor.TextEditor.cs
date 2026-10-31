using Element.Presentation.Overlay.Elements;
using UnityEngine;

namespace Element.Presentation.Layout;

internal sealed partial class OverlayLayoutEditor
{
  private OverlayTextEditor _textEditor;

  private void OpenTextEditor(TextOverlayElement text, bool isNew)
  {
    if (!CommitPopovers())
      return;
    ClosePopovers();
    FinishDrag();
    _toolbarDragging = false;
    _textEditor ??= new OverlayTextEditor(_root.transform, _font, () => _preferences.ReduceUiMotion, _host.Values);
    _textEditor.Open(text, committed => TextEditorClosed(text, isNew, committed));
    UpdateFrames();
  }

  // Done with text keeps it (one undo step); a new text that is cancelled, or
  // any text left empty, is removed.
  private void TextEditorClosed(TextOverlayElement text, bool isNew, bool committed)
  {
    if (committed && !text.IsEmpty)
    {
      if (isNew)
        PlaceNewElement(text);
      else
        _history.Commit(_host.LayoutSettings);
    }
    else if (isNew || committed)
    {
      text.SetEnabled(false);
      _selected = -1;
      if (!isNew)
        _history.Commit(_host.LayoutSettings);
    }
    RefreshLayouts();
    UpdateFrames();
  }
}
