using System;
using System.Collections.Generic;
using Element.Presentation.Layout.Canvas;
using Element.Presentation.Layout.ContentEditors;

namespace Element.Presentation.Layout.Kinds;

/// <summary>The layout editor's element kinds, in the add menu's order.</summary>
internal sealed class ElementKinds : IDisposable
{
  private readonly List<IElementKind> _kinds = new();

  public void Add(IElementKind kind) => _kinds.Add(kind);

  public List<IElementKind> All => _kinds;

  /// <summary>The kind of <paramref name="target"/>, or null.</summary>
  public IElementKind Of(IOverlayLayoutTarget target)
  {
    if (target == null)
      return null;
    for (int i = 0; i < _kinds.Count; i++)
      if (_kinds[i].Owns(target))
        return _kinds[i];
    return null;
  }

  public T Get<T>()
    where T : class, IElementKind
  {
    foreach (IElementKind kind in _kinds)
      if (kind is T typed)
        return typed;
    return null;
  }

  public string Label(IOverlayLayoutTarget target) => Of(target)?.Label(target) ?? target.Label;

  public bool SizedInEditor(IOverlayLayoutTarget target) => Of(target)?.SizedInEditor ?? false;

  public string EditCaption(IOverlayLayoutTarget target) => Of(target)?.EditCaption;

  /// <summary>Opens what edits <paramref name="target"/>; false when nothing does.</summary>
  public bool Edit(IOverlayLayoutTarget target) => Of(target)?.Edit(target) ?? false;

  public bool BuildBar(LayoutElementBar bar, IOverlayLayoutTarget target) => Of(target)?.BuildBar(bar, target) ?? false;

  /// <summary>The full-screen editor that is open, or null.</summary>
  public IContentEditor OpenEditor
  {
    get
    {
      for (int i = 0; i < _kinds.Count; i++)
      {
        IContentEditor editor = _kinds[i].Editor;
        if (editor != null && editor.IsOpen)
          return editor;
      }
      return null;
    }
  }

  /// <summary>How many rows the add menu has now.</summary>
  public int Rows
  {
    get
    {
      int rows = 0;
      for (int i = 0; i < _kinds.Count; i++)
        if (_kinds[i].HasRow)
          rows++;
      return rows;
    }
  }

  public void Dispose()
  {
    foreach (IElementKind kind in _kinds)
      kind.Dispose();
  }
}
