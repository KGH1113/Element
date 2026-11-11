namespace Element.Configuration.Effects;

public sealed class EffectDefinition
{
  public RemovedEffects Flag { get; }
  public EffectGroup Group { get; }
  public string Name { get; }
  public string NameKo { get; }
  public string Description { get; }
  public string DescriptionKo { get; }

  internal EffectDefinition(
    RemovedEffects flag,
    EffectGroup group,
    string name,
    string nameKo,
    string description,
    string descriptionKo
  )
  {
    Flag = flag;
    Group = group;
    Name = name;
    NameKo = nameKo;
    Description = description;
    DescriptionKo = descriptionKo;
  }
}
