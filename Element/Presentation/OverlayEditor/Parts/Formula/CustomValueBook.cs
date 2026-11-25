using System.Collections.Generic;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.Formula;

/// <summary>
/// The values the player made, kept in the settings: each change is saved
/// with them and read back by every element at once. The overlay editor keeps
/// a copy from when it opened, or an element's editor from when it opened, to
/// put back on Cancel (<see cref="Copy"/>, <see cref="Restore"/>).
/// </summary>
internal sealed class CustomValueBook : ICustomValueBook
{
  private readonly ElementSettings _settings;

  public CustomValueBook(ElementSettings settings) => _settings = settings;

  private List<OverlayCustomValueSettings> Values => _settings.OverlayCustomValues ??= new();

  public string Make(OverlayValueType type)
  {
    int number = 1;
    while (Find("v" + number) != null)
      number++;
    string key = "v" + number;
    int shown = Values.Count + 1;
    Values.Add(
      new OverlayCustomValueSettings
      {
        Key = key,
        Name = L("내 값 ", "My value ") + shown,
        Type = OverlayCustomValues.TypeName(type),
      }
    );
    Reload();
    return key;
  }

  public void Write(string key, OverlayVariable variable)
  {
    OverlayCustomValueSettings value = Find(key);
    string text = variable?.Serialize() ?? string.Empty;
    if (value == null || value.Variable == text)
      return;
    value.Variable = text;
    Reload();
  }

  public void Rename(string key, string name)
  {
    OverlayCustomValueSettings value = Find(key);
    name ??= string.Empty;
    if (value == null || value.Name == name)
      return;
    value.Name = name;
    Reload();
  }

  /// <summary>The made values as they are now, to <see cref="Restore"/> later.</summary>
  public List<OverlayCustomValueSettings> Copy()
  {
    List<OverlayCustomValueSettings> copy = new(Values.Count);
    foreach (OverlayCustomValueSettings value in Values)
      copy.Add(value.Clone());
    return copy;
  }

  /// <summary>Puts back the made values of <paramref name="copy"/>; nothing changes when they are the same.</summary>
  public void Restore(List<OverlayCustomValueSettings> copy)
  {
    if (copy == null || Same(copy))
      return;
    Values.Clear();
    foreach (OverlayCustomValueSettings value in copy)
      Values.Add(value.Clone());
    Reload();
  }

  private bool Same(List<OverlayCustomValueSettings> copy)
  {
    List<OverlayCustomValueSettings> values = Values;
    if (copy.Count != values.Count)
      return false;
    for (int i = 0; i < copy.Count; i++)
      if (
        copy[i].Key != values[i].Key
        || copy[i].Name != values[i].Name
        || copy[i].Type != values[i].Type
        || copy[i].Variable != values[i].Variable
      )
        return false;
    return true;
  }

  private OverlayCustomValueSettings Find(string key)
  {
    foreach (OverlayCustomValueSettings value in Values)
      if (value.Key == key)
        return value;
    return null;
  }

  private void Reload() => OverlayCustomValues.Load(Values);
}
