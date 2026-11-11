using Element.Domain.DmNote;
using UnityEngine;

namespace Element.Application.KeyViewer;

/// <summary>
/// The sample the layout editor shows where a key viewer has no live page. It
/// never captures keys or opens IPC sessions.
/// </summary>
internal interface IKeyViewerSample
{
  /// <summary>Sized <see cref="KeyViewerSampleSize"/>; its owner places and scales it.</summary>
  RectTransform Root { get; }

  /// <summary>Tells the player why the live viewer is absent.</summary>
  void SetStatus(NativeViewerStatus status);
}
