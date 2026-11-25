using System;

namespace Element.Domain.Overlay.Text;

/// <summary>
/// The 글자 values of a text: each run of letters side by side that look the
/// same is one value, as it is saved; a value of the game is always its own.
/// The caret belongs to the letters next to it, and a selection stays inside
/// one 글자 value.
/// </summary>
internal static class OverlayTextRuns
{
  /// <summary>
  /// The 글자 value item <paramref name="item"/> is in, from
  /// <paramref name="start"/> up to <paramref name="end"/>; false for a value
  /// of the game or a place past the text.
  /// </summary>
  public static bool RunAt(OverlayTextDocument document, int item, out int start, out int end)
  {
    start = end = item;
    if (item < 0 || item >= document.Count || document[item].IsValue)
      return false;
    OverlayTextItem style = document[item];
    while (start > 0 && Joins(document[start - 1], style))
      start--;
    end = item + 1;
    while (end < document.Count && Joins(document[end], style))
      end++;
    return true;
  }

  /// <summary>
  /// The 글자 value the caret at <paramref name="caret"/> is in: the letters
  /// before it, or with none, the letters after it; false between two values,
  /// where typing starts a new 글자 value.
  /// </summary>
  public static bool RunOfCaret(OverlayTextDocument document, int caret, out int start, out int end) =>
    RunAt(document, caret - 1, out start, out end) || RunAt(document, caret, out start, out end);

  /// <summary>
  /// Where a selection from <paramref name="anchor"/> may reach towards
  /// <paramref name="caret"/>: no further than the 글자 value it started in,
  /// and nowhere from between two values.
  /// </summary>
  public static int Reach(OverlayTextDocument document, int anchor, int caret)
  {
    if (caret == anchor)
      return caret;
    // Towards the caret, the anchor's letters are on the side it moves to.
    bool found =
      caret > anchor
        ? RunAt(document, anchor, out int start, out int end) || RunAt(document, anchor - 1, out start, out end)
        : RunAt(document, anchor - 1, out start, out end) || RunAt(document, anchor, out start, out end);
    return found ? Math.Max(start, Math.Min(end, caret)) : anchor;
  }

  private static bool Joins(OverlayTextItem item, OverlayTextItem style) => !item.IsValue && item.SameStyle(style);
}
