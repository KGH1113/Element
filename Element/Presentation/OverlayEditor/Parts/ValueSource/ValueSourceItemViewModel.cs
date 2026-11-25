using System;
using Element.Domain.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.ValueSource;

/// <summary>One value a color can follow, as its menu offers it: its name, whether it is followed now, and picking it.</summary>
internal sealed class ValueSourceItemViewModel : ViewModel
{
  public ValueSourceItemViewModel(OverlayValueDefinition value, bool followed, Action<OverlayValueDefinition> picked)
  {
    Key = value.Key;
    Name = ValueSectionNames.ValueName(value);
    Followed = followed;
    Pick = Command(() => picked(value));
  }

  public readonly string Key;

  public readonly string Name;

  public readonly bool Followed;

  public Command Pick { get; }
}
