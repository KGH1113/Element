using UnityEngine;

namespace Element.Presentation.Overlay.Canvas;

/// <summary>Ticks the overlay canvas each frame and places its elements again when the viewport changes.</summary>
internal sealed class OverlayViewportWatcher : MonoBehaviour
{
  private OverlayCanvasHost _host;
  private Vector2 _size;
  private UnityEngine.Canvas _canvas;
  private float _scale;
  private bool _viewportDirty = true;

  public void Initialize(OverlayCanvasHost host)
  {
    _host = host;
    _canvas = GetComponent<UnityEngine.Canvas>();
  }

  private void OnEnable() => UnityEngine.Canvas.willRenderCanvases += ApplyViewport;

  private void OnDisable() => UnityEngine.Canvas.willRenderCanvases -= ApplyViewport;

  private void LateUpdate()
  {
    if (_host == null)
      return;
    _host.Tick();
    // The canvas's size, not the screen's: OrbitRender draws the canvas into a
    // video of its own size and aspect while the window stays as it is.
    Vector2 size =
      _canvas != null ? ((RectTransform)_canvas.transform).rect.size : new Vector2(Screen.width, Screen.height);
    float scale = _canvas != null ? _canvas.scaleFactor : 1;
    if (_size == size && _scale == scale)
      return;
    _size = size;
    _scale = scale;
    _viewportDirty = true;
  }

  private void ApplyViewport()
  {
    if (!_viewportDirty || _host == null)
      return;
    _viewportDirty = false;
    // CanvasScaler has applied the viewport scale before this render callback.
    // Reposition only on a viewport change; never force a global Canvas rebuild.
    _host.ApplyLayouts();
  }
}
