namespace Element.Configuration;

[CopiedInPlace]
public sealed class PlanetAppearanceProfile
{
  public bool Enabled = true;
  public PlanetComponentAppearance Body = new();
  public PlanetComponentAppearance Tail = new();
  public PlanetComponentAppearance Ring = new();

  public PlanetAppearanceProfile() { }

  public PlanetAppearanceProfile(string color)
  {
    Body.ColorHex = Tail.ColorHex = Ring.ColorHex = color;
  }

  public void Normalize(string fallback)
  {
    Body ??= new();
    Tail ??= new();
    Ring ??= new();
    Body.Normalize(fallback);
    Tail.Normalize(fallback);
    Ring.Normalize(fallback);
  }
}
