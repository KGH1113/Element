using Element.Configuration.Effects;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Effects;

/// <summary>One effect in the preset editor: its name, what removing it does, and whether the draft removes it.</summary>
internal sealed class EffectChoiceViewModel : ViewModel
{
  public readonly EffectDefinition Definition;
  public readonly string Title;
  public readonly string Description;

  /// <summary>The draft removes this effect.</summary>
  public bool Removed;

  public EffectChoiceViewModel(EffectDefinition definition)
  {
    Definition = definition;
    Title = L(definition.NameKo, definition.Name);
    Description = L(definition.DescriptionKo, definition.Description);
  }
}
