namespace Element.Domain.DmNote;

/// <summary>Whole pixels from the top left.</summary>
internal readonly struct PixelRect
{
  public PixelRect(int x, int y, int width, int height)
  {
    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public int X { get; }
  public int Y { get; }
  public int Width { get; }
  public int Height { get; }

  public override string ToString() => X + "," + Y + " " + Width + "x" + Height;
}
