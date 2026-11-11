using System;
using Element.Presentation.Layout.Canvas;
using Element.Presentation.Layout.ContentEditors;
using Element.Application.Layout;

namespace Element.Presentation.Layout.Kinds;

/// <summary>
/// One kind of overlay element as the layout editor sees it: its name, its
/// row in the add menu, how it is sized, what edits it and its element
/// toolbar. The only place in the editor that knows the kind.
/// </summary>
internal interface IElementKind : IDisposable
{
  bool Owns(IOverlayLayoutTarget target);

  /// <summary>The name the toolbar shows for one selected <paramref name="target"/>.</summary>
  string Label(IOverlayLayoutTarget target);

  /// <summary>Sized in its editor, not by a scale: it only moves, and has an Edit button instead of + and −.</summary>
  bool SizedInEditor { get; }

  /// <summary>The Edit button's caption when it is sized in its editor.</summary>
  string EditCaption { get; }

  /// <summary>Whether it has a row in the add menu now.</summary>
  bool HasRow { get; }

  /// <summary>Its add menu row, made when the menu opens.</summary>
  AddMenuRow Row();

  /// <summary>Opens what edits <paramref name="target"/>: its editor, or ElementNote. False when nothing does.</summary>
  bool Edit(IOverlayLayoutTarget target);

  /// <summary>Fills the element toolbar for <paramref name="target"/> selected alone; false when it has none.</summary>
  bool BuildBar(LayoutElementBar bar, IOverlayLayoutTarget target);

  /// <summary>Its full-screen editor once made; null for none.</summary>
  IContentEditor Editor { get; }
}
