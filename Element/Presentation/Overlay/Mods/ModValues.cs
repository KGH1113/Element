using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using ElementKit.Hosting;
using ElementKit.Values;

namespace Element.Presentation.Overlay.Mods;

/// <summary>
/// The values other mods add through ElementKit, brought into the overlay.
/// They join the catalog when their mod attaches, show their latest value
/// each frame, and step aside while their mod is turned off, keeping their
/// place in texts. A frame with nothing new reads one number per value and
/// allocates nothing.
/// </summary>
internal sealed class ModValues : IOverlayValueFeed
{
  private sealed class Source
  {
    public AttachedMod Mod;
    public bool Active;
    public readonly List<Entry> Entries = new();
  }

  private sealed class Entry
  {
    public Value Value;
    public string Key;
    public OverlayValueGroup Group;
    public OverlayValueId Id = Unplaced;
    public int Pumped = -1;
  }

  private const OverlayValueId Unplaced = (OverlayValueId)(-1);

  private readonly Action<string> _warn;
  private readonly HashSet<string> _warned = new();
  private readonly StringBuilder _text = new(64);
  private List<Source> _sources = new();
  private OverlayValues _values;
  private int _version = -1;

  public ModValues(Action<string> warn) => _warn = warn;

  public void Pump(OverlayValues values)
  {
    if (values == null)
      return;
    if (!ReferenceEquals(values, _values))
    {
      // Another canvas: it gets every value again.
      _values = values;
      foreach (Source source in _sources)
      foreach (Entry entry in source.Entries)
        entry.Pumped = -1;
    }
    int version = KitRegistry.Version;
    if (version != _version)
    {
      _version = version;
      Sync(values);
    }
    for (int i = 0; i < _sources.Count; i++)
    {
      Source source = _sources[i];
      bool active = IsActive(source.Mod);
      if (active != source.Active)
      {
        source.Active = active;
        if (active)
          Place(source);
        else
          Withdraw(source, values);
      }
      if (!active)
        continue;
      List<Entry> entries = source.Entries;
      for (int j = 0; j < entries.Count; j++)
        Write(entries[j], values);
    }
  }

  // The mods attached now; a value that went away keeps its place for texts.
  private void Sync(OverlayValues values)
  {
    List<Source> next = new();
    HashSet<string> kept = new(StringComparer.Ordinal);
    foreach (AttachedMod mod in KitRegistry.Snapshot)
    {
      Source source = new() { Mod = mod, Active = IsActive(mod) };
      foreach (ValueGroup group in mod.Groups)
      {
        OverlayValueGroup heading = new(mod.Id, group.KoreanName, group.EnglishName);
        foreach (Value value in group.Values)
        {
          if (!value.Accepted)
            continue;
          string key = mod.Id + "/" + value.Key;
          if (!kept.Add(key))
            continue;
          source.Entries.Add(
            new Entry
            {
              Value = value,
              Key = key,
              Group = heading,
            }
          );
        }
      }
      next.Add(source);
    }
    foreach (Source old in _sources)
    foreach (Entry entry in old.Entries)
      if (!kept.Contains(entry.Key))
        Withdraw(entry, values);
    _sources = next;
    foreach (Source source in _sources)
      if (source.Active)
        Place(source);
      else
        Withdraw(source, values);
  }

  private void Place(Source source)
  {
    foreach (Entry entry in source.Entries)
    {
      Value value = entry.Value;
      OverlayValueDefinition definition = OverlayValueCatalog.Register(
        entry.Key,
        entry.Group,
        Kind(value),
        value.KoreanName,
        value.EnglishName,
        value switch
        {
          NumberValue number => number.Sample,
          TimeValue time => time.SampleSeconds,
          _ => double.NaN,
        },
        (value as TextValue)?.Sample
      );
      entry.Id = definition?.Id ?? Unplaced;
      entry.Pumped = -1;
      if (definition == null)
        WarnOnce(
          entry.Key,
          "Element can hold "
            + (OverlayValueCatalog.Capacity - OverlayValueCatalog.BuiltInCount)
            + " values from other mods, so "
            + entry.Key
            + " wasn't added. Turn off a mod you don't use to make room."
        );
    }
  }

  private void Withdraw(Source source, OverlayValues values)
  {
    foreach (Entry entry in source.Entries)
      Withdraw(entry, values);
  }

  private static void Withdraw(Entry entry, OverlayValues values)
  {
    OverlayValueCatalog.Withdraw(entry.Key);
    if (entry.Id != Unplaced)
      values.Clear(entry.Id);
    entry.Pumped = -1;
  }

  private void Write(Entry entry, OverlayValues values)
  {
    if (entry.Id == Unplaced)
      return;
    if (entry.Value.Revision == entry.Pumped)
      return;
    entry.Pumped = entry.Value.Read(out bool has, out double number, out string text);
    if (!has)
    {
      values.Clear(entry.Id);
      return;
    }
    _text.Clear();
    switch (entry.Value.Kind)
    {
      case ValueKind.Text:
        // Words show as they are; markup in them is never read.
        OverlayTextComposer.AppendLiteral(_text, text);
        values.Set(entry.Id, _text, double.NaN);
        break;
      case ValueKind.Time:
        OverlayFormulaText.AppendResult(_text, number, true);
        values.Set(entry.Id, _text, number);
        break;
      default:
        OverlayFormulaText.AppendResult(_text, number, false);
        values.Set(entry.Id, _text, number);
        break;
    }
  }

  private bool IsActive(AttachedMod mod)
  {
    try
    {
      return mod.IsActive();
    }
    catch (Exception exception)
    {
      WarnOnce(mod.Id + " active", "Couldn't tell whether " + mod.Id + " is on, so its values are hidden. " + exception.Message);
      return false;
    }
  }

  private static OverlayValueKind Kind(Value value) =>
    value.Kind switch
    {
      ValueKind.Time => OverlayValueKind.Time,
      ValueKind.Text => OverlayValueKind.Text,
      _ => OverlayValueKind.Number,
    };

  private void WarnOnce(string key, string message)
  {
    if (_warned.Add(key))
      _warn?.Invoke(message);
  }
}
