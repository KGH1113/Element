using System;
using System.Threading;

namespace ImplResourcePack.Application.Attempts;

/// <summary>
/// Other mods' rules for keeping a play out of the attempt count. Each mod sets
/// one rule, and every play start asks all of them; nothing is switched off and
/// back on, so a forgotten or failed call cannot leave counting off.
/// </summary>
internal sealed class AttemptSkipRules
{
  private readonly object _gate = new();

  // Replaced, never changed in place, so a play start reads it without a lock.
  private Rule[] _rules = Array.Empty<Rule>();

  public int Count => Volatile.Read(ref _rules).Length;

  /// <summary>Sets <paramref name="owner"/>'s rule, replacing the one it set before.</summary>
  public void Set(string owner, Func<bool> skip)
  {
    if (string.IsNullOrWhiteSpace(owner))
      throw new ArgumentException("Name the mod that sets the rule, such as \"TUFReplay\".", nameof(owner));
    if (skip == null)
      throw new ArgumentNullException(nameof(skip), "Pass a rule, or call RemoveSkipRule to remove one.");

    lock (_gate)
    {
      Rule[] rules = _rules;
      int index = IndexOf(rules, owner);
      Rule[] next;
      if (index >= 0)
        next = (Rule[])rules.Clone();
      else
      {
        next = new Rule[rules.Length + 1];
        Array.Copy(rules, next, rules.Length);
        index = rules.Length;
      }
      next[index] = new Rule(owner, skip);
      Volatile.Write(ref _rules, next);
    }
  }

  public void Remove(string owner) => Remove(owner, null);

  /// <summary>
  /// True when a rule keeps the play that is starting out of the count. A rule that
  /// throws is removed, and the play counts: a broken mod must not stop counting.
  /// </summary>
  public bool Skips(Action<string, Exception> logError)
  {
    foreach (Rule rule in Volatile.Read(ref _rules))
    {
      try
      {
        if (rule.Skip())
          return true;
      }
      catch (Exception exception)
      {
        Remove(rule.Owner, rule.Skip);
        logError?.Invoke(
          "[Attempts] "
            + rule.Owner
            + "'s skip rule failed, so it was removed and plays count again until "
            + rule.Owner
            + " sets it again",
          exception
        );
      }
    }
    return false;
  }

  // With a rule given, removes only that rule: the owner may have set a new one meanwhile.
  private void Remove(string owner, Func<bool> only)
  {
    if (owner == null)
      return;
    lock (_gate)
    {
      Rule[] rules = _rules;
      int index = IndexOf(rules, owner);
      if (index < 0 || (only != null && rules[index].Skip != only))
        return;
      Rule[] next = new Rule[rules.Length - 1];
      Array.Copy(rules, next, index);
      Array.Copy(rules, index + 1, next, index, next.Length - index);
      Volatile.Write(ref _rules, next);
    }
  }

  private static int IndexOf(Rule[] rules, string owner)
  {
    for (int i = 0; i < rules.Length; i++)
      if (string.Equals(rules[i].Owner, owner, StringComparison.Ordinal))
        return i;
    return -1;
  }

  private readonly struct Rule
  {
    public Rule(string owner, Func<bool> skip)
    {
      Owner = owner;
      Skip = skip;
    }

    public string Owner { get; }
    public Func<bool> Skip { get; }
  }
}
