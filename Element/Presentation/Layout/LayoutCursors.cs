using System;
using Element.Domain.Layout;
using Element.Presentation.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Element.Presentation.Layout;

internal sealed class LayoutCursors : IDisposable
{
  private readonly Texture2D[] _textures = new Texture2D[5];
  private int _current = -1;

  public void Set(LayoutHandle handle)
  {
    int index =
      handle == LayoutHandle.Move ? 0
      : handle == LayoutHandle.Left || handle == LayoutHandle.Right ? 1
      : handle == LayoutHandle.Top || handle == LayoutHandle.Bottom ? 2
      : handle == LayoutHandle.TopLeft || handle == LayoutHandle.BottomRight ? 3
      : handle == LayoutHandle.TopRight || handle == LayoutHandle.BottomLeft ? 4
      : -1;
    if (_current == index)
      return;
    _current = index;
    if (index < 0)
    {
      if (!SettingsCursor.Restore())
        SettingsCursor.Apply(null, Vector2.zero);
      return;
    }
    if (_textures[index] == null)
      _textures[index] = Create(index);
    SettingsCursor.Apply(_textures[index], new Vector2(16f, 16f));
  }

  public void Dispose()
  {
    SettingsCursor.Apply(null, Vector2.zero);
    foreach (Texture2D texture in _textures)
      if (texture != null)
        Object.Destroy(texture);
  }

  private static Texture2D Create(int kind)
  {
    bool[] mask = new bool[32 * 32];
    if (kind == 0 || kind == 1)
      Arrow(mask, 5, 16, 27, 16);
    if (kind == 0 || kind == 2)
      Arrow(mask, 16, 5, 16, 27);
    if (kind == 3)
      Arrow(mask, 7, 25, 25, 7);
    if (kind == 4)
      Arrow(mask, 7, 7, 25, 25);
    Color32[] pixels = new Color32[mask.Length];
    for (int y = 1; y < 31; y++)
    for (int x = 1; x < 31; x++)
    {
      bool nearby = false;
      for (int dy = -1; dy <= 1; dy++)
      for (int dx = -1; dx <= 1; dx++)
        nearby |= mask[(y + dy) * 32 + x + dx];
      if (nearby)
        pixels[y * 32 + x] = new Color32(20, 20, 20, 255);
      if (mask[y * 32 + x])
        pixels[y * 32 + x] = new Color32(255, 255, 255, 255);
    }
    Texture2D texture = new(32, 32, TextureFormat.RGBA32, false)
    {
      name = "Overlay layout cursor",
      filterMode = FilterMode.Point,
    };
    texture.SetPixels32(pixels);
    texture.Apply(false, false);
    return texture;
  }

  private static void Arrow(bool[] mask, int x0, int y0, int x1, int y1)
  {
    Line(mask, x0, y0, x1, y1);
    Vector2 direction = new Vector2(x1 - x0, y1 - y0).normalized;
    Vector2 perpendicular = new(-direction.y, direction.x);
    Vector2 start = new(x0, y0);
    Vector2 end = new(x1, y1);
    Vector2 tip = start + direction * 6f + perpendicular * 5f;
    Line(mask, x0, y0, Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y));
    tip = start + direction * 6f - perpendicular * 5f;
    Line(mask, x0, y0, Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y));
    tip = end - direction * 6f + perpendicular * 5f;
    Line(mask, x1, y1, Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y));
    tip = end - direction * 6f - perpendicular * 5f;
    Line(mask, x1, y1, Mathf.RoundToInt(tip.x), Mathf.RoundToInt(tip.y));
  }

  private static void Line(bool[] mask, int x0, int y0, int x1, int y1)
  {
    int dx = Math.Abs(x1 - x0),
      sx = x0 < x1 ? 1 : -1;
    int dy = -Math.Abs(y1 - y0),
      sy = y0 < y1 ? 1 : -1;
    int error = dx + dy;
    while (true)
    {
      if (x0 >= 0 && y0 >= 0 && x0 < 32 && y0 < 32)
        mask[y0 * 32 + x0] = true;
      if (x0 == x1 && y0 == y1)
        break;
      int twice = error * 2;
      if (twice >= dy)
      {
        error += dy;
        x0 += sx;
      }
      if (twice <= dx)
      {
        error += dx;
        y0 += sy;
      }
    }
  }
}
