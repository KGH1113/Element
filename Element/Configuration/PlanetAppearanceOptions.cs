namespace Element.Configuration;

[CopiedInPlace]
public sealed class PlanetAppearanceOptions
{
  public bool Enabled;
  public PlanetAppearanceProfile Red = new("#FF3333");
  public PlanetAppearanceProfile Blue = new("#3380FF");
  public PlanetAppearanceProfile Green = new("#33CC66");

  public void Normalize()
  {
    Red ??= new("#FF3333");
    Blue ??= new("#3380FF");
    Green ??= new("#33CC66");
    Red.Normalize("#FF3333");
    Blue.Normalize("#3380FF");
    Green.Normalize("#33CC66");
  }
}
