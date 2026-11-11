using UnityEngine;

namespace Element.Presentation.Ui.Widgets;

/// <summary>
/// The sliced rounded-corner sprite behind every settings panel and button.
/// Made once and shared by every <see cref="UiFactory"/>; it goes when
/// the last one is disposed.
/// </summary>
internal static class UiRoundedSprite
{
  private const int Size = 40;
  private const float Radius = 16f;
  private static Texture2D s_texture;
  private static Sprite s_sprite;
  private static int s_users;

  public static Sprite Retain()
  {
    s_users++;
    // Also made again when something destroyed it, as the UI preview does between renders.
    if (s_sprite == null)
      Create();
    return s_sprite;
  }

  public static void Release()
  {
    if (--s_users > 0)
      return;
    s_users = 0;
    UiFactory.Destroy(s_sprite);
    UiFactory.Destroy(s_texture);
    s_sprite = null;
    s_texture = null;
  }

  private static void Create()
  {
    UiFactory.Destroy(s_texture);
    s_texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Settings rounded corners" };
    Color[] pixels = new Color[Size * Size];
    for (int y = 0; y < Size; y++)
    for (int x = 0; x < Size; x++)
    {
      float dx = Mathf.Max(Radius - x - .5f, x + .5f - (Size - Radius));
      float dy = Mathf.Max(Radius - y - .5f, y + .5f - (Size - Radius));
      float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
      pixels[y * Size + x] = new Color(1, 1, 1, Mathf.Clamp01(Radius - distance));
    }
    s_texture.SetPixels(pixels);
    s_texture.Apply(false, true);
    s_sprite = Sprite.Create(
      s_texture,
      new Rect(0, 0, Size, Size),
      new Vector2(.5f, .5f),
      100,
      0,
      SpriteMeshType.FullRect,
      new Vector4(18, 18, 18, 18)
    );
  }
}
