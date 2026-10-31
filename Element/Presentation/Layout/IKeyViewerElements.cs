using System.Collections.Generic;

namespace Element.Presentation.Layout;

internal enum KeyViewerAvailability
{
  Ready,
  Unsupported,
  AppClosed,
  HiddenInApp,
}

/// <summary>ElementNote's in-game key viewers, as the layout editor adds and switches them.</summary>
internal interface IKeyViewerElements
{
  IReadOnlyList<IOverlayLayoutTarget> Targets { get; }

  /// <summary>Built-in viewers: kept hidden when deleted, so Reset all can bring them back.</summary>
  IReadOnlyCollection<string> DefaultIds { get; }

  KeyViewerAvailability Availability { get; }

  /// <param name="templateId">OverlayElementKinds.HandKeyViewer or FootKeyViewer.</param>
  IOverlayLayoutTarget Add(string templateId);

  /// <summary>
  /// Replaces <paramref name="target"/> with the other kind in the same place.
  /// The old viewer is hidden, not removed, so undo can bring it back.
  /// </summary>
  IOverlayLayoutTarget Switch(IOverlayLayoutTarget target, string templateId);
}
