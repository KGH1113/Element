namespace Element.Domain.Layout;

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
