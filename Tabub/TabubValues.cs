using ElementKit.Values;

namespace Tabub;

/// <summary>
/// What Tabub hands to Element: keys per group, written the way the
/// TabubuLabubu tag writes them. Players put these in their own texts.
/// </summary>
public static class TabubValues
{
  public static readonly ValueGroup Group = new("타법", "Tabub");

  /// <summary>The groups played so far, in pairs, four pairs a line, as the tag shows them.</summary>
  public static readonly TextValue Played = Group.Text("tabub", "타법", "Tabub", "44 44 32 32");

  /// <summary>The groups still to come, from the pair the next hit is in, four lines at most.</summary>
  public static readonly TextValue Ahead = Group.Text("ahead", "앞으로 타법", "Tabub ahead", "44 44 32 32");

  /// <summary>The pair the next hit is in, such as "44" or "[10 8]".</summary>
  public static readonly TextValue Pair = Group.Text("pair", "지금 타법", "Pair now", "44");

  /// <summary>The pair after it.</summary>
  public static readonly TextValue NextPair = Group.Text("next-pair", "다음 타법", "Next pair", "32");

  /// <summary>Keys in the group the next hit is in.</summary>
  public static readonly NumberValue Keys = Group.Number("keys", "이번 박 키", "Keys this beat", 4);

  /// <summary>Nothing to show: outside a level.</summary>
  internal static void Clear()
  {
    Played.Clear();
    Ahead.Clear();
    Pair.Clear();
    NextPair.Clear();
    Keys.Clear();
  }
}
