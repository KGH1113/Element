using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using ElementKit.Values;

namespace ElementKit.Hosting;

// A mod that attached, with the value groups found in it.
internal sealed class AttachedMod
{
  public AttachedMod(string id, IReadOnlyList<ValueGroup> groups, Func<bool> isActive)
  {
    Id = id;
    Groups = groups;
    IsActive = isActive ?? (() => true);
  }

  public string Id { get; }
  public IReadOnlyList<ValueGroup> Groups { get; }

  // Element hides a turned-off mod's values; UMM keeps its assembly loaded.
  public Func<bool> IsActive { get; }
}

/// <summary>
/// The mods that attached and what they add. Element reads it every frame it
/// draws and rebuilds its catalog when <see cref="Version"/> moves. Attaching
/// the same mod again, as a UMM reload does, replaces its earlier entry.
/// </summary>
internal static class KitRegistry
{
  private static readonly object Sync = new();
  private static readonly List<AttachedMod> Mods = new();
  private static AttachedMod[] _snapshot = Array.Empty<AttachedMod>();
  private static int _version;

  public static int Version => Volatile.Read(ref _version);

  /// <summary>The attached mods in the order they first attached; never changes after it is returned.</summary>
  public static AttachedMod[] Snapshot => Volatile.Read(ref _snapshot);

  public static AttachedMod Attach(string modId, Assembly assembly, Func<bool> isActive, Action<string> report)
  {
    report ??= _ => { };
    if (!ValidModId(modId))
    {
      report(
        "[ElementKit] The mod id \""
          + modId
          + "\" can't name values. Start it with a letter and use letters, digits, '.', '_' and '-'; nothing was added."
      );
      return null;
    }
    List<ValueGroup> groups = ValueScanner.Find(assembly, report);
    Validate(groups, report);
    foreach (ValueGroup group in groups)
      group.ModId = modId;
    AttachedMod attached = new(modId, groups, isActive);
    lock (Sync)
    {
      int index = Mods.FindIndex(mod => mod.Id == modId);
      if (index >= 0)
        Mods[index] = attached;
      else
        Mods.Add(attached);
      Publish();
    }
    return attached;
  }

  public static void Detach(string modId)
  {
    lock (Sync)
    {
      if (Mods.RemoveAll(mod => mod.Id == modId) > 0)
        Publish();
    }
  }

  /// <summary>Forgets every mod; for tests.</summary>
  internal static void Clear()
  {
    lock (Sync)
    {
      Mods.Clear();
      Publish();
    }
  }

  private static void Publish()
  {
    Volatile.Write(ref _snapshot, Mods.ToArray());
    Interlocked.Increment(ref _version);
  }

  // The rules that keep values easy for players: plain names in both
  // languages, an example for the editor and a key texts can keep.
  private static void Validate(List<ValueGroup> groups, Action<string> report)
  {
    HashSet<string> keys = new(StringComparer.Ordinal);
    foreach (ValueGroup group in groups)
    {
      bool named = !string.IsNullOrWhiteSpace(group.KoreanName) && !string.IsNullOrWhiteSpace(group.EnglishName);
      if (!named)
        report(
          "[ElementKit] A value group needs a Korean and an English heading, such as new ValueGroup(\"타법\", \"Fingering\"). Its values were skipped."
        );
      foreach (Value value in group.Values.ToArray())
      {
        value.Accepted = named && Accept(value, keys, report);
        if (value.Accepted)
          keys.Add(value.Key);
      }
    }
  }

  private static bool Accept(Value value, HashSet<string> keys, Action<string> report)
  {
    string name = "\"" + value.Key + "\"";
    if (!ValidKey(value.Key))
    {
      report(
        "[ElementKit] The value key "
          + name
          + " can't be saved in texts. Start it with a lowercase letter and use lowercase letters, digits and dashes, such as \"left-keys\". The value was skipped."
      );
      return false;
    }
    if (keys.Contains(value.Key))
    {
      report("[ElementKit] Two values use the key " + name + ". Give each value its own key; the second one was skipped.");
      return false;
    }
    if (string.IsNullOrWhiteSpace(value.KoreanName) || string.IsNullOrWhiteSpace(value.EnglishName))
    {
      report(
        "[ElementKit] The value " + name + " needs a short Korean and English name, such as \"왼손 키\" and \"Left keys\". The value was skipped."
      );
      return false;
    }
    bool sample = value switch
    {
      NumberValue number => IsFinite(number.Sample),
      TimeValue time => IsFinite(time.SampleSeconds),
      TextValue text => !string.IsNullOrEmpty(text.Sample),
      _ => false,
    };
    if (!sample)
    {
      report(
        "[ElementKit] The value " + name + " needs an example so the editor can show it before your mod sets it. The value was skipped."
      );
      return false;
    }
    return true;
  }

  private static bool IsFinite(double number) => !double.IsNaN(number) && !double.IsInfinity(number);

  // Matches Element's own rule for a mod's key: ModId/key.
  internal static bool ValidKey(string key)
  {
    if (string.IsNullOrEmpty(key) || key.Length > 48 || key[0] < 'a' || key[0] > 'z' || key[key.Length - 1] == '-')
      return false;
    foreach (char c in key)
      if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-'))
        return false;
    return true;
  }

  internal static bool ValidModId(string id)
  {
    if (string.IsNullOrEmpty(id) || id.Length > 46 || !IsAsciiLetter(id[0]))
      return false;
    foreach (char c in id)
      if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '_' && c != '-' && c != '.')
        return false;
    return true;
  }

  private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
}
