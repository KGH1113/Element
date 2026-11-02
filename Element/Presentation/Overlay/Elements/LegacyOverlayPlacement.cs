using Element.Domain.Elements;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Overlay.Elements;

// Where each built-in overlay drew before it became a text element, in
// 1920x1080 canvas units at its own font size. A converted text keeps this
// place until it is moved, so an untouched layout looks exactly as before.
internal readonly struct LegacyOverlayPlacement
{
  public readonly Vector2 Anchor;
  public readonly Vector2 Pivot;
  public readonly Vector2 Position;

  // Zero fits the rectangle to the text, like the old combo's ContentSizeFitter.
  public readonly Vector2 Size;
  public readonly TextAlignmentOptions Alignment;
  private readonly bool _valid;

  private LegacyOverlayPlacement(
    Vector2 anchor,
    Vector2 pivot,
    Vector2 position,
    Vector2 size,
    TextAlignmentOptions alignment
  )
  {
    Anchor = anchor;
    Pivot = pivot;
    Position = position;
    Size = size;
    Alignment = alignment;
    _valid = true;
  }

  public bool FitsText => Size == Vector2.zero;

  // The old combo element: its title and number are fitted inside this rectangle.
  public static readonly LegacyOverlayPlacement ComboRoot = new(
    new Vector2(.5f, 1),
    new Vector2(.5f, 1),
    new Vector2(0, -57),
    new Vector2(300, 200),
    TextAlignmentOptions.Center
  );

  public static bool TryGet(string id, TMP_FontAsset font, out LegacyOverlayPlacement placement)
  {
    Vector2 topLeft = new(0, 1),
      topRight = new(1, 1),
      top = new(.5f, 1),
      bottom = new(.5f, 0),
      center = new(.5f, .5f);
    Vector2 row = new(456, 30);
    placement = id switch
    {
      // Status rows: a 456x135 block 16 units from the top left.
      "progress" => new(topLeft, center, new Vector2(244, -31), row, TextAlignmentOptions.TopLeft),
      "accuracy" => new(topLeft, center, new Vector2(244, -66), row, TextAlignmentOptions.TopLeft),
      "time" => new(topLeft, center, new Vector2(244, -101), row, TextAlignmentOptions.TopLeft),
      "fps" => new(topLeft, center, new Vector2(244, -136), row, TextAlignmentOptions.TopLeft),
      // BPM rows: 16 units from the top right.
      "tile-bpm" => new(topRight, topRight, new Vector2(-16, -16), row, TextAlignmentOptions.TopRight),
      "current-bpm" => new(topRight, topRight, new Vector2(-16, -51), row, TextAlignmentOptions.TopRight),
      "keys-per-second" => new(topRight, topRight, new Vector2(-16, -86), row, TextAlignmentOptions.TopRight),
      // Just under the progress bar, which ends 28 units from the top.
      "song-title" => new(top, top, new Vector2(0, -30), new Vector2(600, 32), TextAlignmentOptions.Center),
      // The number centers 45% up the combo block; the title sits on its top edge.
      OverlayElementDefaults.ComboId => new(top, center, new Vector2(0, -167), Vector2.zero, TextAlignmentOptions.Top),
      OverlayElementDefaults.ComboTitleId => new(
        top,
        new Vector2(.5f, 0),
        new Vector2(0, -123.495f),
        Vector2.zero,
        TextAlignmentOptions.Center
      ),
      "judgement" => new(bottom, bottom, new Vector2(0, 85), new Vector2(1000, 30), TextAlignmentOptions.Bottom),
      "timing-scale" => new(bottom, bottom, new Vector2(0, 130), new Vector2(300, 30), TextAlignmentOptions.Bottom),
      // JRP writes both attempt lines into one text; the first is one line up.
      "attempt" => new(
        bottom,
        bottom,
        new Vector2(310, 35 + LineAdvance(font)),
        new Vector2(300, 30),
        TextAlignmentOptions.BottomLeft
      ),
      "full-attempt" => new(
        bottom,
        bottom,
        new Vector2(310, 35),
        new Vector2(300, 30),
        TextAlignmentOptions.BottomLeft
      ),
      _ => default,
    };
    return placement._valid;
  }

  private static float LineAdvance(TMP_FontAsset font)
  {
    if (font == null)
      return OverlayElementDefaults.TextSize;
    UnityEngine.TextCore.FaceInfo face = font.faceInfo;
    return face.pointSize > 0f
      ? face.lineHeight / face.pointSize * face.scale * OverlayElementDefaults.TextSize
      : OverlayElementDefaults.TextSize;
  }
}
