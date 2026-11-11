using UnityEngine;

namespace Element.Application.KeyViewer;

/// <summary>Makes the samples the layout editor shows in place of key viewers.</summary>
internal interface IKeyViewerSamples
{
  IKeyViewerSample Create(string kind, Transform parent);
}
