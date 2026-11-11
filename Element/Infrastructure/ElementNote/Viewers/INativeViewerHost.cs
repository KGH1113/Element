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

  /// <summary>Normalized top-left panels; each radius is normalized to the view height.</summary>
  void ExcludeEditor(IReadOnlyList<LayoutExclusion> normalized);
  void ApplyHighlight(Color accent);
}
