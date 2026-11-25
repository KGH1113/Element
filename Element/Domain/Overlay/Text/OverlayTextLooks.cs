using System.Collections.Generic;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Conditions;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;

namespace Element.Domain.Overlay.Text;

/// <summary>
/// Which runs a text shows now: each If among its runs gives way to the runs
/// of its first case that holds, or of Otherwise. The conditions are parsed
/// once (<see cref="Load"/>); <see cref="Update"/> checks them again and
/// lists the runs anew only when an If changed its case, so a text whose
/// values change on every hit costs a few comparisons. A text without Ifs
/// shows its own runs, untouched.
/// </summary>
internal sealed class OverlayTextLooks
{
  private readonly List<Choice> _choices = new();
  private readonly List<OverlayTextRun> _shown = new();
  private List<OverlayTextRun> _runs;

  /// <summary>The runs to draw now; none of them is an If.</summary>
  public IReadOnlyList<OverlayTextRun> Shown => _choices.Count > 0 ? _shown : _runs;

  /// <summary>The values the conditions use.</summary>
  public OverlayValueMask Mask { get; private set; }

  public bool HasIfs => _choices.Count > 0;

  /// <summary>Takes a text's runs, with every If on Otherwise until <see cref="Update"/>.</summary>
  public void Load(List<OverlayTextRun> runs)
  {
    _runs = runs;
    _choices.Clear();
    _shown.Clear();
    Mask = OverlayValueMask.None;
    if (runs == null)
      return;
    foreach (OverlayTextRun run in runs)
    {
      if (run?.If == null)
        continue;
      Choice choice = new(run.If);
      Mask |= choice.Mask;
      _choices.Add(choice);
    }
    if (_choices.Count > 0)
      List();
  }

  /// <summary>Checks every condition against <paramref name="source"/>; true when what is shown changed.</summary>
  public bool Update(IOverlayNumberSource source)
  {
    bool changed = false;
    for (int i = 0; i < _choices.Count; i++)
      changed |= _choices[i].Pick(source);
    if (changed)
      List();
    return changed;
  }

  /// <summary>
  /// The first of <paramref name="conditions"/> that holds, or their count
  /// (Otherwise) when none does.
  /// </summary>
  public static int Pick(IReadOnlyList<OverlayConditionCheck> conditions, IOverlayNumberSource source)
  {
    for (int i = 0; i < conditions.Count; i++)
      if (conditions[i].Holds(source))
        return i;
    return conditions.Count;
  }

  private void List()
  {
    _shown.Clear();
    int next = 0;
    foreach (OverlayTextRun run in _runs)
    {
      if (run == null)
        continue;
      if (run.If == null)
      {
        _shown.Add(run);
        continue;
      }
      List<OverlayTextRun> look = _choices[next++].Runs;
      if (look == null)
        continue;
      // An If inside a case is not a thing; one that got there shows nothing.
      foreach (OverlayTextRun inner in look)
        if (inner != null && inner.If == null)
          _shown.Add(inner);
    }
  }

  private sealed class Choice
  {
    private readonly OverlayTextIf _if;
    private readonly List<OverlayConditionCheck> _checks = new();
    private int _picked;

    public Choice(OverlayTextIf source)
    {
      _if = source;
      if (source.Cases != null)
        foreach (OverlayTextCase item in source.Cases)
        {
          OverlayConditionCheck check = new(item?.When);
          Mask |= check.Mask;
          _checks.Add(check);
        }
      _picked = _checks.Count;
    }

    public OverlayValueMask Mask { get; }

    public List<OverlayTextRun> Runs => _picked < _checks.Count ? _if.Cases[_picked]?.Runs : _if.Otherwise;

    public bool Pick(IOverlayNumberSource source)
    {
      int picked = OverlayTextLooks.Pick(_checks, source);
      if (picked == _picked)
        return false;
      _picked = picked;
      return true;
    }
  }
}
