using System;
using System.Collections.Generic;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using UnityEngine;

namespace Element.Infrastructure.Game.DmNote;

/// <summary>Geometry the editor's layout target reads from and writes to a native viewer.</summary>
internal interface INativeViewerLayoutHost
{
  ulong GeometryRevision { get; }
  bool HasSurface(string kind);
  void Select(string kind, bool selected);
  void Layout(string kind, bool customized, float centerX, float centerY, float scale);
  void LayoutBounds(string kind, Rect normalizedBounds, float effectiveScale);
  bool TryGetReferenceSize(string kind, out Vector2 size);

  /// <summary>The viewer's content at its natural CSS size, as the separate ElementNote window shows it.</summary>
  bool TryGetContentSize(string kind, out Vector2 size);

  /// <summary>The game view in desktop units (macOS points, Windows DIPs): one CSS pixel each at 100%.</summary>
  bool TryGetViewSize(out Vector2 size);

  bool TryGetBounds(string kind, out Rect normalizedBounds, out float effectiveScale);
  bool TryGetSelectionBounds(string kind, out Rect normalizedBounds);
  bool TryGetPointer(out Vector2 normalized);
}

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
