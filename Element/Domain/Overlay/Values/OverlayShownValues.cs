using System.Collections.Generic;
using System.Text;

namespace Element.Domain.Overlay.Values;

/// <summary>
/// What each animated value of one text showed last, to tell when one shows
/// something new. What counts is the text as drawn, not the number behind it:
/// elapsed time changes every frame but reads differently once a second. A
/// value that appears (it had no data) or goes away is no change, so the
/// overlay filling in as a level starts doesn't make everything grow.
/// </summary>
internal sealed class OverlayShownValues
{
  private readonly List<Shown> _slots = new();

  /// <summary>Forgets everything; the next update of each slot only records.</summary>
  public void Clear() => _slots.Clear();

  /// <summary>
  /// Records that slot <paramref name="slot"/> now shows <paramref name="length"/>
  /// characters of <paramref name="text"/> from <paramref name="start"/>.
  /// True when it showed something before, shows something now, and the two differ.
  /// </summary>
  public bool Update(int slot, StringBuilder text, int start, int length)
  {
    if (slot < 0)
      return false;
    while (_slots.Count <= slot)
      _slots.Add(default);
    Shown before = _slots[slot];
    Shown now = length > 0 ? new Shown(length, Hash(text, start, length)) : default;
    _slots[slot] = now;
    return before.Length > 0 && now.Length > 0 && (before.Length != now.Length || before.Hash != now.Hash);
  }

  // FNV-1a over the characters. With the length beside it, two different
  // values practically never read as the same.
  private static uint Hash(StringBuilder text, int start, int length)
  {
    uint hash = 2166136261;
    int end = start + length;
    for (int i = start; i < end; i++)
    {
      hash ^= text[i];
      hash *= 16777619;
    }
    return hash;
  }

  private readonly struct Shown
  {
    public readonly int Length;
    public readonly uint Hash;

    public Shown(int length, uint hash)
    {
      Length = length;
      Hash = hash;
    }
  }
}
