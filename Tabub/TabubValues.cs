using ElementKit.Values;

namespace Tabub;

/// <summary>
/// What Tabub hands to Element. Players put these in their own texts and
/// shapes, such as "왼손 {왼손 키}키 · {지금 타법}".
/// </summary>
public static class TabubValues
{
  public static readonly ValueGroup Group = new("타법", "Tabub");

  /// <summary>The hand that plays the next hit.</summary>
  public static readonly TextValue Hand = Group.Text("hand", "지금 손", "Hand", "왼손");

  /// <summary>Keys per press in the hand's stretch, such as "2 + 2 + 1".</summary>
  public static readonly TextValue Pattern = Group.Text("pattern", "지금 타법", "Pattern", "2 + 2 + 1");

  /// <summary>Inputs in the hand's stretch: one key per hit.</summary>
  public static readonly NumberValue Keys = Group.Number("keys", "지금 손 키", "Keys now", 5);

  /// <summary>Inputs of the left hand's stretch, now or next.</summary>
  public static readonly NumberValue LeftKeys = Group.Number("left-keys", "왼손 키", "Left keys", 5);

  /// <summary>Inputs of the right hand's stretch, now or next.</summary>
  public static readonly NumberValue RightKeys = Group.Number("right-keys", "오른손 키", "Right keys", 8);

  /// <summary>The hand of the stretch after this one.</summary>
  public static readonly TextValue NextHand = Group.Text("next-hand", "다음 손", "Next hand", "오른손");

  /// <summary>The pattern of the stretch after this one.</summary>
  public static readonly TextValue NextPattern = Group.Text("next-pattern", "다음 타법", "Next pattern", "2 + 2 + 2 + 2");

  /// <summary>Nothing to show: outside a level, or after its last hit.</summary>
  internal static void Clear()
  {
    Hand.Clear();
    Pattern.Clear();
    Keys.Clear();
    LeftKeys.Clear();
    RightKeys.Clear();
    NextHand.Clear();
    NextPattern.Clear();
  }
}
