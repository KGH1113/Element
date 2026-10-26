using System;
using ImplResourcePack.Domain.DmNote;
using UnityEngine;

namespace ImplResourcePack.Infrastructure.Game.DmNote;

/// <summary>Geometry the editor's layout target reads from and writes to a native viewer.</summary>
internal interface INativeViewerLayoutHost
{
  ulong GeometryRevision { get; }
  bool HasSurface(string kind);
  void Select(string kind, bool selected);
  void Layout(string kind, bool customized, float centerX, float centerY, float scale);
  void LayoutBounds(string kind, Rect normalizedBounds, float effectiveScale);
  bool TryGetReferenceSize(string kind, out Vector2 size);
  bool TryGetBounds(string kind, out Rect normalizedBounds, out float effectiveScale);
  bool TryGetSelectionBounds(string kind, out Rect normalizedBounds);
  bool TryGetPointer(out Vector2 normalized);
}

/// <summary>The in-game page host. All members run on Unity's main thread.</summary>
internal interface INativeViewerHost : INativeViewerLayoutHost, IDisposable
{
  string LoadError { get; }
  void Apply((string Id, NativeViewerSurface Surface)[] surfaces);
  void Show(bool visible);
  void Edit(bool editing);
  void ExcludeEditor(Rect normalizedBounds);
  void ApplyHighlight(Color accent);
}
