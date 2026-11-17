using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.Canvas.ElementBar;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>
/// A kind edited in a full-screen editor: it is sized there, opens it from
/// Edit, a double-click or Enter, and decides what a finished or cancelled
/// edit leaves behind.
/// </summary>
internal abstract class ContentElementKind<T> : IElementKind
  where T : class, IOverlayLayoutTarget
{
  protected readonly ICanvasEditing Editing;
  private IContentEditor _editor;

  protected ContentElementKind(ICanvasEditing editing) => Editing = editing;

  public IContentEditor Editor => _editor;

  public bool Owns(IOverlayLayoutTarget target) => target is T;

  public abstract string Label(IOverlayLayoutTarget target);

  public bool SizedInEditor => true;

  public abstract string EditCaption { get; }

  public virtual bool HasRow => true;

  public abstract AddMenuRow Row();

  public bool Edit(IOverlayLayoutTarget target)
  {
    if (target is not T typed)
      return false;
    Open(typed, false);
    return true;
  }

  public ElementBarContent Bar(IOverlayLayoutTarget target) => null;

  protected abstract IContentEditor MakeEditor();

  /// <summary>Opens <paramref name="target"/> in the editor; <paramref name="isNew"/> when it was just added.</summary>
  internal void Open(T target, bool isNew)
  {
    if (!Editing.Settle())
      return;
    _editor ??= MakeEditor();
    if (!_editor.Open(target, committed => Closed(target, isNew, committed)))
    {
      Refused(target);
      return;
    }
    Editing.UpdateFrames();
  }

  /// <summary>The editor could not open <paramref name="target"/>.</summary>
  protected virtual void Refused(T target) { }

  /// <summary>What a finished (<paramref name="committed"/>) or cancelled edit leaves behind.</summary>
  protected abstract void Closed(T target, bool isNew, bool committed);

  /// <summary>
  /// The usual end of an edit: done is one undo step; a new element that is
  /// cancelled was never placed, so undo has nothing to restore, and goes.
  /// </summary>
  protected void KeepOrDrop(T target, OverlayLayoutSettings settings, bool isNew, bool committed)
  {
    if (committed)
      Editing.Session.Commit();
    else if (isNew)
    {
      if (settings != null)
        Domain.Overlay.Defaults.OverlayElementDefaults.ResetPlacement(settings);
      target.SetEnabled(false);
      Editing.Selection.Selected = -1;
    }
    Editing.RefreshLayouts();
    Editing.UpdateFrames();
  }

  public void Dispose() => _editor?.Dispose();
}
