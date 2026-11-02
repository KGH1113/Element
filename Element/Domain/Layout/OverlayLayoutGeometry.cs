using System;

namespace Element.Domain.Layout;

internal enum LayoutHandle
{
  None,
  Move,
  Left,
  Right,
  Top,
  Bottom,
  TopLeft,
  TopRight,
  BottomLeft,
  BottomRight,
}

// Screen pixels, with the origin in the top-left. Kept independent of Unity so
// proportional resize behavior can be tested without starting the game.
internal readonly struct LayoutBounds
{
  public readonly float X;
  public readonly float Y;
  public readonly float Width;
  public readonly float Height;
  public float Right => X + Width;
  public float Bottom => Y + Height;

  public LayoutBounds(float x, float y, float width, float height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }
}

internal static class OverlayLayoutGeometry
{
  public const float MinimumVisibleFraction = .1f;

  public static LayoutHandle HitTest(LayoutBounds bounds, float x, float y, float tolerance = 8f)
  {
    if (
      x < bounds.X - tolerance
      || x > bounds.Right + tolerance
      || y < bounds.Y - tolerance
      || y > bounds.Bottom + tolerance
    )
      return LayoutHandle.None;

    bool left = Math.Abs(x - bounds.X) <= tolerance;
    bool right = Math.Abs(x - bounds.Right) <= tolerance;
    bool top = Math.Abs(y - bounds.Y) <= tolerance;
    bool bottom = Math.Abs(y - bounds.Bottom) <= tolerance;
    if (top && left)
      return LayoutHandle.TopLeft;
    if (top && right)
      return LayoutHandle.TopRight;
    if (bottom && left)
      return LayoutHandle.BottomLeft;
    if (bottom && right)
      return LayoutHandle.BottomRight;
    if (left)
      return LayoutHandle.Left;
    if (right)
      return LayoutHandle.Right;
    if (top)
      return LayoutHandle.Top;
    if (bottom)
      return LayoutHandle.Bottom;
    return LayoutHandle.Move;
  }

  public static LayoutBounds Drag(
    LayoutBounds start,
    LayoutHandle handle,
    float deltaX,
    float deltaY,
    float screenWidth,
    float screenHeight,
    float minimumScale = 0.2f,
    float maximumScale = 5f
  )
  {
    if (start.Width <= 0f || start.Height <= 0f || !Finite(deltaX) || !Finite(deltaY))
      return start;
    if (handle == LayoutHandle.None)
      return start;
    if (handle == LayoutHandle.Move)
      return KeepVisible(
        new LayoutBounds(start.X + deltaX, start.Y + deltaY, start.Width, start.Height),
        screenWidth,
        screenHeight
      );

    float directionX =
      handle == LayoutHandle.Left || handle == LayoutHandle.TopLeft || handle == LayoutHandle.BottomLeft ? -1f
      : handle == LayoutHandle.Right || handle == LayoutHandle.TopRight || handle == LayoutHandle.BottomRight ? 1f
      : 0f;
    float directionY =
      handle == LayoutHandle.Top || handle == LayoutHandle.TopLeft || handle == LayoutHandle.TopRight ? -1f
      : handle == LayoutHandle.Bottom || handle == LayoutHandle.BottomLeft || handle == LayoutHandle.BottomRight ? 1f
      : 0f;

    // Project a corner gesture onto the original diagonal. Edge gestures use
    // their own axis. Both preserve the opposite anchor and the aspect ratio.
    float vx = start.Width * directionX;
    float vy = start.Height * directionY;
    float ratio = 1f + (deltaX * vx + deltaY * vy) / (vx * vx + vy * vy);
    LimitScale(start, directionX, directionY, screenWidth, screenHeight, ref minimumScale, ref maximumScale);
    ratio = Clamp(ratio, minimumScale, maximumScale);
    float width = start.Width * ratio;
    float height = start.Height * ratio;
    float x =
      directionX < 0f ? start.Right - width
      : directionX > 0f ? start.X
      : start.X + (start.Width - width) * 0.5f;
    float y =
      directionY < 0f ? start.Bottom - height
      : directionY > 0f ? start.Y
      : start.Y + (start.Height - height) * 0.5f;
    return new LayoutBounds(x, y, width, height);
  }

  public static LayoutBounds KeepVisible(LayoutBounds bounds, float screenWidth, float screenHeight) =>
    new(
      Clamp(
        bounds.X,
        -bounds.Width * (1 - MinimumVisibleFraction),
        screenWidth - bounds.Width * MinimumVisibleFraction
      ),
      Clamp(
        bounds.Y,
        -bounds.Height * (1 - MinimumVisibleFraction),
        screenHeight - bounds.Height * MinimumVisibleFraction
      ),
      bounds.Width,
      bounds.Height
    );

  public static LayoutBounds ScaleAtCenter(
    LayoutBounds start,
    float ratio,
    float screenWidth,
    float screenHeight,
    float minimumScale,
    float maximumScale
  )
  {
    if (start.Width <= 0 || start.Height <= 0 || !Finite(ratio))
      return start;
    LimitScale(start, 0, 0, screenWidth, screenHeight, ref minimumScale, ref maximumScale);
    ratio = Clamp(ratio, minimumScale, maximumScale);
    float width = start.Width * ratio,
      height = start.Height * ratio;
    return new LayoutBounds(
      start.X + (start.Width - width) * .5f,
      start.Y + (start.Height - height) * .5f,
      width,
      height
    );
  }

  private static void LimitScale(
    LayoutBounds start,
    float directionX,
    float directionY,
    float screenWidth,
    float screenHeight,
    ref float minimum,
    ref float maximum
  )
  {
    LimitAxis(start.X, start.Width, screenWidth, directionX, ref minimum, ref maximum);
    LimitAxis(start.Y, start.Height, screenHeight, directionY, ref minimum, ref maximum);
    maximum = Math.Max(.001f, maximum);
    minimum = Math.Max(.001f, Math.Min(minimum, maximum));
  }

  private static void LimitAxis(
    float position,
    float size,
    float screen,
    float direction,
    ref float minimum,
    ref float maximum
  )
  {
    float visible = MinimumVisibleFraction;
    maximum = Math.Min(maximum, screen / (size * visible));
    if (direction < 0)
    {
      maximum = Math.Min(maximum, (position + size) / (size * visible));
      minimum = Math.Max(minimum, (position + size - screen) / (size * (1 - visible)));
    }
    else if (direction > 0)
    {
      maximum = Math.Min(maximum, (screen - position) / (size * visible));
      minimum = Math.Max(minimum, -position / (size * (1 - visible)));
    }
    else
    {
      float center = position + size * .5f;
      minimum = Math.Max(minimum, Math.Max(-center, center - screen) / (size * (.5f - visible)));
    }
  }

  public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

  private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}
