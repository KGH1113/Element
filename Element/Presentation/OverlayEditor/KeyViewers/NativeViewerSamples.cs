using Element.Application.KeyViewer;
using TMPro;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.KeyViewers;

/// <summary>The expanded samples, with a status line, that the layout editor shows in place of live key viewers.</summary>
internal sealed class NativeViewerSamples : IKeyViewerSamples
{
  private readonly TMP_FontAsset _font;

  public NativeViewerSamples(TMP_FontAsset font) => _font = font;

  public IKeyViewerSample Create(string kind, Transform parent) =>
    NativeViewerMock.Create(kind, parent, _font, true).GetComponent<NativeViewerMock>();
}
