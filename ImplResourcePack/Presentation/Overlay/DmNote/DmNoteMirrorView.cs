using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ImplResourcePack.Presentation.Overlay.DmNote;

internal sealed class DmNoteMirrorView : IDisposable
{
  private const float MaxWidth = 430f;
  private const float MaxHeight = 270f;
  private readonly GameObject _root;
  private readonly Viewer _hand;
  private readonly Viewer _foot;

  private sealed class Viewer
  {
    public RawImage Image;
    public Texture2D Texture;
    public int Width;
    public int Height;
  }

  public DmNoteMirrorView(Shared.OverlayCanvasHost host)
  {
    _root = new GameObject("ImplDmNote in-game viewers", typeof(RectTransform));
    _root.transform.SetParent(host.Root.transform, false);
    RectTransform rootRect = _root.GetComponent<RectTransform>();
    rootRect.anchorMin = Vector2.zero;
    rootRect.anchorMax = Vector2.one;
    rootRect.offsetMin = Vector2.zero;
    rootRect.offsetMax = Vector2.zero;
    _hand = CreateViewer("Hand", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f));
    _foot = CreateViewer("Foot", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f));
    _root.SetActive(false);
  }

  public void SetVisible(bool visible)
  {
    if (_root != null)
      _root.SetActive(visible);
  }

  public void HideMissing(bool hasHand, bool hasFoot)
  {
    if (!hasHand)
      _hand.Image.enabled = false;
    if (!hasFoot)
      _foot.Image.enabled = false;
  }

  public void Apply(string viewerKind, int width, int height, string format, byte[] pixels)
  {
    if (pixels == null || width <= 0 || height <= 0 || width > 4096 || height > 4096)
      return;
    Viewer viewer =
      viewerKind == "hand" ? _hand
      : viewerKind == "foot" ? _foot
      : null;
    if (viewer == null)
      return;

    if (viewer.Texture == null || viewer.Width != width || viewer.Height != height)
    {
      if (viewer.Texture != null)
        Object.Destroy(viewer.Texture);
      viewer.Texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
      viewer.Texture.wrapMode = TextureWrapMode.Clamp;
      viewer.Texture.filterMode = FilterMode.Bilinear;
      viewer.Width = width;
      viewer.Height = height;
      viewer.Image.texture = viewer.Texture;
      float scale = Mathf.Min(1f, MaxWidth / width, MaxHeight / height);
      viewer.Image.rectTransform.sizeDelta = new Vector2(width * scale, height * scale);
    }

    if (format == "rgba")
    {
      if (pixels.Length != (long)width * height * 4)
        return;
      viewer.Texture.LoadRawTextureData(pixels);
      viewer.Texture.Apply(false, false);
    }
    else if (format == "png")
    {
      if (!ImageConversion.LoadImage(viewer.Texture, pixels, false))
        return;
    }
    else
      return;

    viewer.Image.enabled = true;
  }

  public void Dispose()
  {
    if (_hand.Texture != null)
      Object.Destroy(_hand.Texture);
    if (_foot.Texture != null)
      Object.Destroy(_foot.Texture);
    if (_root != null)
      Object.Destroy(_root);
  }

  private Viewer CreateViewer(string name, Vector2 anchor, Vector2 pivot, Vector2 position)
  {
    GameObject go = new(name, typeof(RectTransform), typeof(RawImage));
    go.transform.SetParent(_root.transform, false);
    RectTransform rect = go.GetComponent<RectTransform>();
    rect.anchorMin = anchor;
    rect.anchorMax = anchor;
    rect.pivot = pivot;
    rect.anchoredPosition = position;
    RawImage image = go.GetComponent<RawImage>();
    image.raycastTarget = false;
    image.enabled = false;
    return new Viewer { Image = image };
  }
}
