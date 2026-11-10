using System;

namespace Element.Domain.DmNote;

/// <summary>
/// The part of a key viewer frame a video shows: the page minus its padding, in
/// whole texels. A crop that starts between texels makes the scaler blend
/// neighbours and softens every frame.
/// </summary>
internal readonly struct RenderFrameCrop
{
  private readonly int _frameWidth;
  private readonly int _frameHeight;
  private readonly float _viewportWidth;
  private readonly float _viewportHeight;

  private RenderFrameCrop(int frameWidth, int frameHeight, float viewportWidth, float viewportHeight, int x, int y)
  {
    _frameWidth = frameWidth;
    _frameHeight = frameHeight;
    _viewportWidth = viewportWidth;
    _viewportHeight = viewportHeight;
    X = x;
    Y = y;
    Width = frameWidth - 2 * x;
    Height = frameHeight - 2 * y;
  }

  /// <param name="frameWidth">The frame in texels; ElementNote draws the whole viewport into it.</param>
  /// <param name="viewportWidth">The page's CSS viewport.</param>
  /// <param name="padding">CSS pixels around the content on each side.</param>
  public static RenderFrameCrop Of(
    int frameWidth,
    int frameHeight,
    float viewportWidth,
    float viewportHeight,
    float padding
  ) =>
    new(
      frameWidth,
      frameHeight,
      viewportWidth,
      viewportHeight,
      Inset(padding, frameWidth, viewportWidth),
      Inset(padding, frameHeight, viewportHeight)
    );

  /// <summary>Texels cut from the left and from the right.</summary>
  public int X { get; }

  /// <summary>Texels cut from the top and from the bottom.</summary>
  public int Y { get; }

  /// <summary>The texels left to show.</summary>
  public int Width { get; }
  public int Height { get; }

  /// <summary>The shown texels in the page's CSS pixels.</summary>
  public float CssWidth => Width * _viewportWidth / _frameWidth;
  public float CssHeight => Height * _viewportHeight / _frameHeight;

  // Keeps at least one texel.
  private static int Inset(float padding, int texels, float cssPixels) =>
    Math.Max(0, Math.Min((int)Math.Round(padding * texels / cssPixels), (texels - 1) / 2));
}
