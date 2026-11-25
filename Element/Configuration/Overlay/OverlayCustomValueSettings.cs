using System;

namespace Element.Configuration.Overlay;

// A value the player made, which every element of the overlay can use: its
// name, what it gives and its variable (see OverlayVariable), such as
// "if({accuracy};>=;100;{highlight-color};#FFFFFF)". Elements use it as
// "{@Key}". Type is "number", "text", "color", "bool" or "any".
[Serializable]
[CopiedInPlace]
public sealed class OverlayCustomValueSettings
{
  public string Key = string.Empty;
  public string Name = string.Empty;
  public string Type = "any";
  public string Variable = string.Empty;

  public OverlayCustomValueSettings Clone() =>
    new()
    {
      Key = Key ?? string.Empty,
      Name = Name ?? string.Empty,
      Type = Type ?? "any",
      Variable = Variable ?? string.Empty,
    };
}
