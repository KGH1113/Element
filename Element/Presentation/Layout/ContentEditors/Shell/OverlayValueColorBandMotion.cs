using System;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Shell;

/// <summary>
/// Runs a value color band's glide to its new scale once a frame, while it is
/// enabled; the band turns it off once it arrives.
/// </summary>
internal sealed class OverlayValueColorBandMotion : MonoBehaviour
{
  /// <summary>One frame of the glide, given the seconds since the last.</summary>
  public Action<float> Step;

  private void Update() => Step?.Invoke(Time.unscaledDeltaTime);
}
