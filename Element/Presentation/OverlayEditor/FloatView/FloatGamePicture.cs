using System;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using UnityEngine.Rendering;

namespace Element.Presentation.OverlayEditor.FloatView;

/// <summary>Copies the composited game camera before screen-space overlay canvases draw.</summary>
internal sealed class FloatGamePicture : IDisposable
{
  private readonly Camera _camera;
  private readonly CommandBuffer _copy;
  public RenderTexture Texture { get; }

  public FloatGamePicture(int width, int height)
  {
    foreach (Camera candidate in Camera.allCameras)
      if (
        candidate.targetTexture == null
        && candidate.targetDisplay == 0
        && candidate.cameraType == CameraType.Game
        && (_camera == null || candidate.depth > _camera.depth)
      )
        _camera = candidate;
    if (_camera == null)
      return;
    float scale = Mathf.Min(1, 1920f / width, 1080f / height);
    Texture = new RenderTexture(
      Mathf.Max(1, Mathf.RoundToInt(width * scale)),
      Mathf.Max(1, Mathf.RoundToInt(height * scale)),
      0
    )
    {
      name = "Float view game picture",
    };
    Texture.Create();
    _copy = new CommandBuffer { name = Texture.name };
    _copy.Blit(BuiltinRenderTextureType.CameraTarget, Texture);
    _camera.AddCommandBuffer(CameraEvent.AfterEverything, _copy);
  }

  public void Dispose()
  {
    if (_camera != null && _copy != null)
      _camera.RemoveCommandBuffer(CameraEvent.AfterEverything, _copy);
    _copy?.Release();
    if (Texture != null)
    {
      Texture.Release();
      UiFactory.Destroy(Texture);
    }
  }
}
