using System;

namespace ImplResourcePack.Domain.Editor;

internal enum EditorTransformKind
{
  Rotate,
  FlipHorizontal,
  FlipVertical,
}

internal static class EditorTransform
{
  // Legacy paths only have fifteen-degree steps. Relative and midspin codes
  // deliberately stay untouched rather than changing their meaning.
  private const string LegacyDirections = "RpJEToUqHQGWLxNZFVDYBCMA";

  public static char RotateLegacy(char direction, bool clockwise)
  {
    int index = LegacyDirections.IndexOf(direction);
    return index < 0 ? direction : LegacyDirections[(index + (clockwise ? 23 : 1)) % 24];
  }

  public static float RotateAngle(float angle, float delta) => angle == 999 ? angle : angle + delta;

  public static (float X, float Y) Position(float x, float y, EditorTransformKind kind, float degrees)
  {
    if (kind == EditorTransformKind.FlipHorizontal)
      return (-x, y);
    if (kind == EditorTransformKind.FlipVertical)
      return (x, -y);
    // A blank component is an unchanged axis. Rotation requires both axes;
    // use zero for that missing offset, matching the editor's offset origin.
    if (float.IsNaN(x))
      x = 0;
    if (float.IsNaN(y))
      y = 0;
    double radians = degrees * Math.PI / 180;
    double cosine = Math.Cos(radians),
      sine = Math.Sin(radians);
    return ((float)(x * cosine - y * sine), (float)(x * sine + y * cosine));
  }
}
