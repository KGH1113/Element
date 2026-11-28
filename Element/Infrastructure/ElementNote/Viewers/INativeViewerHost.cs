using System;
using System.Collections.Generic;
using Element.Domain.KeyViewer;
using Element.Domain.Layout;
using UnityEngine;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>The in-game page host. All members run on Unity's main thread.</summary>
internal interface INativeViewerHost : INativeViewerLayoutHost, IDisposable
{
  /// <summary>The latest page or host problem, already mapped to a player-facing status.</summary>
  bool TryGetFault(out NativeViewerStatus status, out string message);
  void Apply((string Id, NativeViewerSurface Surface)[] surfaces);
  void Show(bool visible);
  void Edit(bool editing);

  /// <summary>The size Unity draws the game at, in its pixels: the host covers only where the game is drawn.</summary>
  void FitGame(int width, int height);

  /// <summary>Normalized top-left panels; each radius is normalized to the view height.</summary>
  void ExcludeEditor(IReadOnlyList<LayoutExclusion> normalized);

  /// <summary>
  /// What overlay elements in front of the viewer <paramref name="kind"/> draw
  /// over it: normalized top-left rectangles that don't overlap, left out of
  /// its page; empty shows all of it. A library without covers ignores them.
  /// </summary>
  void Cover(string kind, IReadOnlyList<LayoutBounds> normalized);
  void ApplyHighlight(Color accent);
}
