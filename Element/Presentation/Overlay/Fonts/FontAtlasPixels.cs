using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Element.Presentation.Overlay.Fonts;

/// <summary>
/// The alpha of font atlases on the CPU, so the key viewers behind a text can
/// trace its glyphs. A readable atlas (TMP's dynamic ones, which fonts loaded
/// at run time are) is read in place, never copied. An atlas the GPU alone
/// holds is copied once each time it changes, in the background
/// (AsyncGPUReadback, no stall); until that copy arrives, or where the GPU
/// can't read it back, texts fall back to their glyph boxes.
/// </summary>
internal sealed class FontAtlasPixels : IDisposable
{
  private readonly Dictionary<Texture2D, Copy> _copies = new();
  private readonly List<Texture2D> _gone = new();
  private bool _disposed;

  /// <summary>Counts up when a copy arrives: texts traced from boxes until then can trace their glyphs.</summary>
  public int Version { get; private set; }

  /// <summary>
  /// The alpha of <paramref name="texture"/>, read in place or from its copy;
  /// false while there is none (asks for the copy then).
  /// </summary>
  public bool TryRead(Texture texture, out FontAtlasAlpha alpha)
  {
    alpha = default;
    if (
      _disposed
      || texture is not Texture2D atlas
      || atlas == null
      || !Layout(atlas.format, out int step, out int offset)
    )
      return false;
    int width = atlas.width,
      height = atlas.height;
    if (atlas.isReadable)
    {
      NativeArray<byte> data = atlas.GetPixelData<byte>(0);
      if (!data.IsCreated || data.Length < width * height * step)
        return false;
      alpha = new FontAtlasAlpha(data, width, height, step, offset);
      return true;
    }
    if (!_copies.TryGetValue(atlas, out Copy copy))
    {
      Sweep();
      copy = new Copy();
      _copies.Add(atlas, copy);
    }
    uint updates = atlas.updateCount;
    if (!copy.Failed && !copy.Pending && (!copy.Data.IsCreated || copy.Updates != updates))
      Request(atlas, copy, updates);
    if (!copy.Data.IsCreated || copy.Width != width || copy.Height != height)
      return false;
    alpha = new FontAtlasAlpha(copy.Data, width, height, step, offset);
    return true;
  }

  // Bytes per texel and where alpha sits, for the formats TMP's atlases use.
  private static bool Layout(TextureFormat format, out int step, out int offset)
  {
    (step, offset) = format switch
    {
      TextureFormat.Alpha8 or TextureFormat.R8 => (1, 0),
      TextureFormat.RGBA32 or TextureFormat.BGRA32 => (4, 3),
      TextureFormat.ARGB32 => (4, 0),
      _ => (0, 0),
    };
    return step > 0;
  }

  private void Request(Texture2D atlas, Copy copy, uint updates)
  {
    if (!SystemInfo.supportsAsyncGPUReadback)
    {
      copy.Failed = true;
      return;
    }
    copy.Pending = true;
    copy.Updates = updates;
    int width = atlas.width,
      height = atlas.height;
    // Once per atlas change, so the callback may allocate its closure.
    AsyncGPUReadback.Request(atlas, 0, request => Arrived(copy, request, width, height));
  }

  private void Arrived(Copy copy, AsyncGPUReadbackRequest request, int width, int height)
  {
    copy.Pending = false;
    if (_disposed)
      return;
    if (request.hasError)
    {
      copy.Failed = true;
      return;
    }
    NativeArray<byte> data = request.GetData<byte>();
    if (copy.Data.IsCreated)
      copy.Data.Dispose();
    copy.Data = new NativeArray<byte>(data, Allocator.Persistent);
    copy.Width = width;
    copy.Height = height;
    Version++;
  }

  // Copies of atlases destroyed since (a font let go) go with them.
  private void Sweep()
  {
    _gone.Clear();
    foreach (KeyValuePair<Texture2D, Copy> pair in _copies)
      if (pair.Key == null && !pair.Value.Pending)
        _gone.Add(pair.Key);
    foreach (Texture2D atlas in _gone)
    {
      if (_copies[atlas].Data.IsCreated)
        _copies[atlas].Data.Dispose();
      _copies.Remove(atlas);
    }
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    foreach (Copy copy in _copies.Values)
      if (copy.Data.IsCreated)
        copy.Data.Dispose();
    _copies.Clear();
  }

  private sealed class Copy
  {
    public NativeArray<byte> Data;
    public int Width,
      Height;
    public uint Updates;
    public bool Pending,
      Failed;
  }
}
