using System;
using Element.Application.KeyViewer;
using UnityEngine;
using Element.Domain.KeyViewer;

namespace Element.Infrastructure.ElementNote.Viewers;

/// <summary>
/// The offline sample the layout editor shows in a key viewer's place while the
/// viewer has no live page, on a canvas of its own made the first time it shows.
/// </summary>
internal sealed class NativeViewerSamplePreview : IDisposable
{
  private readonly string _kind;
  private readonly Func<NativeViewerStatus> _status;
  private IKeyViewerSamples _samples;
  private IKeyViewerSample _sample;
  private GameObject _canvas;
  private RectTransform _root;
  private Rect _bounds = new(float.NaN, 0, 0, 0);

  /// <param name="status">What the sample says; null for no status line.</param>
  public NativeViewerSamplePreview(string kind, Func<NativeViewerStatus> status)
  {
    _kind = kind;
    _status = status;
  }

  /// <summary>Where the sample comes from; nothing shows until there is a source.</summary>
  public void Configure(IKeyViewerSamples samples) => _samples = samples;

  /// <summary>Every editor frame: shows or hides the sample, placed over <paramref name="bounds"/>.</summary>
  public void Tick(bool visible, Rect bounds)
  {
    if (visible && _canvas == null && _samples != null)
    {
      _canvas = new GameObject("ElementNote offline preview", typeof(RectTransform), typeof(Canvas));
      if (UnityEngine.Application.isPlaying)
        UnityEngine.Object.DontDestroyOnLoad(_canvas);
      Canvas canvas = _canvas.GetComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 32758;
      _sample = _samples.Create(_kind, _canvas.transform);
      _root = _sample.Root;
      _bounds = new Rect(float.NaN, 0, 0, 0);
    }
    if (_canvas == null)
      return;
    if (_canvas.activeSelf != visible)
      _canvas.SetActive(visible);
    if (!visible)
      return;
    // Runs every editor frame; touching the RectTransform would rebuild the
    // preview canvas each frame even when nothing moved.
    if (_bounds != bounds)
    {
      _bounds = bounds;
      _root.anchorMin = _root.anchorMax = new Vector2(0, 1);
      _root.pivot = new Vector2(0, 1);
      _root.anchoredPosition = new Vector2(bounds.x, -bounds.y);
      _root.localScale = new Vector3(
        bounds.width / KeyViewerSampleSize.Width(_kind),
        bounds.height / KeyViewerSampleSize.ViewportHeight,
        1
      );
    }
    if (_status != null)
    {
      // The sample is shown only while this viewer has no live page, so an
      // attached session means ElementNote has no tab for this kind.
      NativeViewerStatus status = _status();
      _sample?.SetStatus(status == NativeViewerStatus.Attached ? NativeViewerStatus.NoViewerTab : status);
    }
  }

  public void Dispose()
  {
    if (_canvas != null)
    {
      if (UnityEngine.Application.isPlaying)
        UnityEngine.Object.Destroy(_canvas);
      else
        UnityEngine.Object.DestroyImmediate(_canvas);
    }
    _canvas = null;
    _root = null;
    _sample = null;
  }
}
