namespace Element.Configuration;

[CopiedInPlace]
public sealed class PlanetComponentAppearance
{
  public bool OverrideColor = true;
  public string ColorHex = "#FFFFFF";
  public float Opacity = 100;

  public void Normalize(string fallback)
  {
    Opacity = GameplayTweaksSettings.FiniteClamp(Opacity, 0, 100, 100);
    string hex = ColorHex?.Trim().TrimStart('#');
    if (hex == null || hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _))
      ColorHex = fallback;
    else
      ColorHex = "#" + hex.ToUpperInvariant();
  }
}
