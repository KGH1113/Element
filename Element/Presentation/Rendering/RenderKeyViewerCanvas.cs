using System;
using System.Collections.Generic;
using System.IO;
using Element.Application.Rendering;
using Element.Domain.DmNote;
using Element.Domain.Layout;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Element.Presentation.Rendering;

/// <summary>
/// ElementNote's key viewers as images on a canvas OrbitRender records, each
/// where its viewer sits in the video. Unity main thread.
/// </summary>
internal sealed class RenderKeyViewerCanvas : IDisposable
{
  // Above the overlay, as the in-game viewers sit above the game; below Element's settings and notices.
  private const int SortingOrder = 32000;

  private readonly GameObject _root;
  private readonly List<Slot> _slots = new();
  private readonly Dictionary<string, Page> _pages = new(StringComparer.Ordinal);

  public RenderKeyViewerCanvas(IReadOnlyList<RenderViewerPlacement> placements)
  {
    _root = new GameObject("Element Render Key Viewers", typeof(RectTransform));
    if (UnityEngine.Application.isPlaying)
      Object.DontDestroyOnLoad(_root);
    Canvas = _root.AddComponent<Canvas>();
    Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    Canvas.sortingOrder = SortingOrder;
    foreach (RenderViewerPlacement placement in placements)
    {
      var image = new GameObject(placement.ViewerKind + " key viewer", typeof(RectTransform)).AddComponent<RawImage>();
      image.transform.SetParent(_root.transform, false);
      image.raycastTarget = false;
      image.enabled = false;
      _slots.Add(new Slot(placement, image));
    }
  }

  public Canvas Canvas { get; }

  /// <summary>Sizes each image to its page's content and crops the page's padding away.</summary>
  public void Configure(IReadOnlyList<RenderSurface> surfaces)
  {
    foreach (RenderSurface surface in surfaces)
      if (!_pages.ContainsKey(surface.ViewerKind))
        _pages.Add(surface.ViewerKind, new Page(surface));
    foreach (Slot slot in _slots)
    {
      if (!_pages.TryGetValue(slot.Placement.ViewerKind, out Page page))
        continue;
      RenderSurface surface = page.Surface;
      LayoutBounds bounds = slot.Placement.Place(
        surface.ViewportWidth - surface.Padding * 2,
        surface.ViewportHeight - surface.Padding * 2
      );
      RectTransform rect = slot.Image.rectTransform;
      // Anchors are fractions of the video, so the image follows whatever size OrbitRender draws at.
      rect.anchorMin = new Vector2(bounds.X, 1 - bounds.Y - bounds.Height);
      rect.anchorMax = new Vector2(bounds.X + bounds.Width, 1 - bounds.Y);
      rect.offsetMin = rect.offsetMax = Vector2.zero;
      (float x, float y, float width, float height) = RenderViewerPlacement.ContentUv(
        surface.ViewportWidth,
        surface.ViewportHeight,
        surface.Padding,
        surface.FrameFormat == "rgba"
      );
      slot.Image.uvRect = new Rect(x, y, width, height);
      slot.Image.texture = page.Texture;
    }
  }

  /// <summary>Loads each viewer's frame; false with the reason when one cannot be read.</summary>
  public bool Show(IReadOnlyList<RenderFrame> frames, out string error)
  {
    foreach (RenderFrame frame in frames)
    {
      if (!_pages.TryGetValue(frame.ViewerKind, out Page page))
      {
        error = "No page was prepared for the " + frame.ViewerKind + " key viewer.";
        return false;
      }
      if (!page.Load(frame.FramePath, out error))
        return false;
    }
    foreach (Slot slot in _slots)
      slot.Image.enabled = slot.Image.texture != null;
    error = null;
    return true;
  }

  public void Hide()
  {
    foreach (Slot slot in _slots)
      slot.Image.enabled = false;
  }

  public void Dispose()
  {
    if (_root != null)
      Object.Destroy(_root);
    foreach (Page page in _pages.Values)
      page.Dispose();
    _pages.Clear();
    _slots.Clear();
  }

  private sealed class Slot
  {
    public Slot(RenderViewerPlacement placement, RawImage image)
    {
      Placement = placement;
      Image = image;
    }

    public RenderViewerPlacement Placement { get; }
    public RawImage Image { get; }
  }

  // One texture per viewer kind; several viewers of a kind show the same page.
  private sealed class Page : IDisposable
  {
    private byte[] _pixels;

    public Page(RenderSurface surface)
    {
      Surface = surface;
      Texture = new Texture2D(surface.Width, surface.Height, TextureFormat.RGBA32, false)
      {
        name = "Element render " + surface.ViewerKind + " key viewer",
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
        hideFlags = HideFlags.HideAndDontSave,
      };
    }

    public RenderSurface Surface { get; }
    public Texture2D Texture { get; }

    public bool Load(string path, out string error)
    {
      try
      {
        if (Surface.FrameFormat == "png")
        {
          if (!ImageConversion.LoadImage(Texture, File.ReadAllBytes(path), false))
          {
            error = "A key viewer frame is not a readable PNG.";
            return false;
          }
        }
        else if (!LoadRaw(path, out error))
          return false;
        error = null;
        return true;
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        error = "A key viewer frame could not be read: " + exception.Message;
        return false;
      }
    }

    // Reuses one buffer: a 60 fps render reads a frame per viewer every video frame.
    private bool LoadRaw(string path, out string error)
    {
      int length = Surface.Width * Surface.Height * 4;
      _pixels ??= new byte[length];
      using (
        var stream = new FileStream(
          path,
          FileMode.Open,
          FileAccess.Read,
          FileShare.ReadWrite | FileShare.Delete,
          1 << 16
        )
      )
      {
        if (stream.Length != length)
        {
          error = "A key viewer frame has an unexpected size.";
          return false;
        }
        int read = 0;
        while (read < length)
        {
          int count = stream.Read(_pixels, read, length - read);
          if (count <= 0)
            break;
          read += count;
        }
        if (read != length)
        {
          error = "A key viewer frame ended early.";
          return false;
        }
      }
      Texture.LoadRawTextureData(_pixels);
      Texture.Apply(false, false);
      error = null;
      return true;
    }

    public void Dispose() => Object.Destroy(Texture);
  }
}
