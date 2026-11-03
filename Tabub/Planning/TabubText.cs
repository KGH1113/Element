using System;
using System.Collections.Generic;
using System.Text;

namespace Tabub.Planning;

/// <summary>
/// Tabub written the way the TabubuLabubu tag writes it: groups in pairs such
/// as <c>44</c>, or <c>[10 8]</c> when either side is over 8, a space after
/// each pair and a new line after every four pairs.
/// </summary>
public static class TabubText
{
  /// <summary>The most groups the played text keeps; past it, the oldest <see cref="Drop"/> go.</summary>
  public const int Window = 34;

  public const int Drop = 8;
  public const int PairsPerLine = 4;

  /// <summary>
  /// What the tag shows once <paramref name="played"/> groups are done: the
  /// latest of them, the last pair appearing once the group after it is done.
  /// </summary>
  public static string Played(IReadOnlyList<int> counts, int played, StringBuilder builder)
  {
    played = Math.Max(0, Math.Min(played, counts.Count));
    // The tag drops the oldest eight groups whenever it holds more than 34.
    int start = played <= Window ? 0 : Drop * (int)Math.Ceiling((played - Window) / (double)Drop);
    int length = played - start;
    builder.Clear();
    for (int i = 0; i < length; i++)
    {
      if (i % 2 == 0 && i > 0)
        AppendPair(builder, counts[start + i - 2], counts[start + i - 1]);
      if (i % (PairsPerLine * 2) == 0 && i > 0)
        builder.Append('\n');
    }
    return builder.ToString();
  }

  /// <summary>
  /// The groups still to come, from the pair the next hit is in, lined up
  /// with <see cref="Played"/>: up to <paramref name="groups"/> groups.
  /// </summary>
  public static string Ahead(IReadOnlyList<int> counts, int current, int groups, StringBuilder builder)
  {
    builder.Clear();
    int start = current - current % 2;
    int end = Math.Min(counts.Count, start + groups);
    int pairs = 0;
    for (int i = start; i < end; i += 2)
    {
      if (pairs > 0 && pairs % PairsPerLine == 0)
        builder.Append('\n');
      AppendPair(builder, counts[i], i + 1 < counts.Count ? counts[i + 1] : 0);
      pairs++;
    }
    return builder.ToString().TrimEnd(' ');
  }

  /// <summary>The pair group <paramref name="group"/> is in, such as "44"; null past the end.</summary>
  public static string Pair(IReadOnlyList<int> counts, int group)
  {
    int first = group - group % 2;
    if (group < 0 || first >= counts.Count)
      return null;
    StringBuilder builder = new(8);
    AppendPair(builder, counts[first], first + 1 < counts.Count ? counts[first + 1] : 0);
    return builder.ToString().TrimEnd(' ');
  }

  private static void AppendPair(StringBuilder builder, int first, int second)
  {
    if (first > 8 || second > 8)
      builder.Append('[').Append(first).Append(' ').Append(second).Append("] ");
    else
      builder.Append(first).Append(second).Append(' ');
  }
}
