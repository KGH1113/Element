using ElementUI;
using UnityEngine;

namespace Element.Presentation.Settings.Judgment;

/// <summary>One judgment popup on the judgment page: its name, its color, whether it is hidden, and the click that flips it.</summary>
internal sealed class JudgmentChoice
{
  public readonly string Name;
  public readonly Color Tint;
  public readonly Computed<bool> Hidden;
  public readonly Command Toggle;

  public JudgmentChoice(string name, Color tint, Computed<bool> hidden, Command toggle)
  {
    Name = name;
    Tint = tint;
    Hidden = hidden;
    Toggle = toggle;
  }
}
