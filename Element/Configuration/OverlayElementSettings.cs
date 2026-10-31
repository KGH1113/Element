using System;
using System.Collections.Generic;

namespace Element.Configuration;

public static class OverlayElementKinds
{
  public const string Text = "text";
  public const string Shape = "shape";
  public const string HandKeyViewer = "native-hand";
  public const string FootKeyViewer = "native-foot";

  public static bool IsKeyViewer(string templateId) => templateId == HandKeyViewer || templateId == FootKeyViewer;
}

// Every overlay color either follows the highlight color, keeping only its own
// opacity, or is a fixed custom color.
[Serializable]
public sealed class OverlayColorSettings
{
  public bool Highlight;
  public string Hex = "#FFFFFF";
  public float Alpha = 1f;

  public static OverlayColorSettings HighlightColor(float alpha = 1f) => new() { Highlight = true, Alpha = alpha };

  public static OverlayColorSettings Custom(string hex, float alpha = 1f) => new() { Hex = hex, Alpha = alpha };

  public OverlayColorSettings Clone() =>
    new()
    {
      Highlight = Highlight,
      Hex = Hex,
      Alpha = Alpha,
    };

  public bool SameAs(OverlayColorSettings other) =>
    other != null && Highlight == other.Highlight && Alpha == other.Alpha && (Highlight || Hex == other.Hex);
}

// One run is either literal text in one color, or one value (Value is its key).
[Serializable]
public sealed class OverlayTextRun
{
  public string Text = string.Empty;
  public string Value = string.Empty;
  public OverlayColorSettings Color = new();

  // Font size in reference pixels before the element's scale; zero is the
  // text's base size.
  public float Size;

  public bool IsValue => !string.IsNullOrEmpty(Value);

  public static OverlayTextRun Literal(string text, OverlayColorSettings color) =>
    new() { Text = text ?? string.Empty, Color = color };

  public static OverlayTextRun ValueRun(string key, OverlayColorSettings color) => new() { Value = key, Color = color };

  public OverlayTextRun Clone() =>
    new()
    {
      Text = Text,
      Value = Value,
      Color = (Color ?? new OverlayColorSettings()).Clone(),
      Size = Size,
    };

  public bool SameAs(OverlayTextRun other) =>
    other != null
    && Text == other.Text
    && Value == other.Value
    && Size == other.Size
    && (Color ?? new OverlayColorSettings()).SameAs(other.Color ?? new OverlayColorSettings());
}

[Serializable]
public sealed class OverlayTextSettings
{
  public List<OverlayTextRun> Runs = new();

  public OverlayTextSettings Clone()
  {
    OverlayTextSettings copy = new();
    if (Runs != null)
      foreach (OverlayTextRun run in Runs)
        if (run != null)
          copy.Runs.Add(run.Clone());
    return copy;
  }

  public bool SameAs(OverlayTextSettings other)
  {
    if (other == null)
      return false;
    int count = Runs?.Count ?? 0;
    if (count != (other.Runs?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Runs[i]?.SameAs(other.Runs[i]) ?? other.Runs[i] == null))
        return false;
    return true;
  }
}

public enum OverlayShapeKind
{
  Square,
  Triangle,
  Circle,
}

// X and Y are fractions of the shape's box; Radius is in reference units.
[Serializable]
public sealed class OverlayShapePoint
{
  public float X;
  public float Y;
  public float Radius;

  public OverlayShapePoint Clone() =>
    new()
    {
      X = X,
      Y = Y,
      Radius = Radius,
    };

  public bool SameAs(OverlayShapePoint other) =>
    other != null && X == other.X && Y == other.Y && Radius == other.Radius;
}

// Width, Height and StrokeWidth are reference units (1920x1080) before the
// element's Scale, which grows the whole shape uniformly.
[Serializable]
public sealed class OverlayShapeSettings
{
  public OverlayShapeKind Kind = OverlayShapeKind.Square;
  public float Width = 160f;
  public float Height = 160f;
  public List<OverlayShapePoint> Points = new();
  public OverlayColorSettings Fill = OverlayColorSettings.HighlightColor();
  public OverlayColorSettings Stroke = OverlayColorSettings.Custom("#FFFFFF");
  public float StrokeWidth;

  public OverlayShapeSettings Clone()
  {
    OverlayShapeSettings copy = new()
    {
      Kind = Kind,
      Width = Width,
      Height = Height,
      Fill = (Fill ?? OverlayColorSettings.HighlightColor()).Clone(),
      Stroke = (Stroke ?? OverlayColorSettings.Custom("#FFFFFF")).Clone(),
      StrokeWidth = StrokeWidth,
    };
    if (Points != null)
      foreach (OverlayShapePoint point in Points)
        if (point != null)
          copy.Points.Add(point.Clone());
    return copy;
  }

  public bool SameAs(OverlayShapeSettings other)
  {
    if (
      other == null
      || Kind != other.Kind
      || Width != other.Width
      || Height != other.Height
      || StrokeWidth != other.StrokeWidth
      || !(Fill?.SameAs(other.Fill) ?? other.Fill == null)
      || !(Stroke?.SameAs(other.Stroke) ?? other.Stroke == null)
    )
      return false;
    int count = Points?.Count ?? 0;
    if (count != (other.Points?.Count ?? 0))
      return false;
    for (int i = 0; i < count; i++)
      if (!(Points[i]?.SameAs(other.Points[i]) ?? other.Points[i] == null))
        return false;
    return true;
  }
}
