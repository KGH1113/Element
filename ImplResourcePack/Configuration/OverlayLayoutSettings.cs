using System;

namespace ImplResourcePack.Configuration;

public enum Anchor9
{
  TopLeft,
  Top,
  TopRight,
  Left,
  Center,
  Right,
  BottomLeft,
  Bottom,
  BottomRight,
}

[Serializable]
public sealed class OverlayLayoutSettings
{
  public string Id = string.Empty;

  // Empty on legacy, built-in elements; copies identify their source separately.
  public string TemplateId = string.Empty;

  // Field initialization also supplies the migration default for older XML saves.
  public bool Enabled = true;
  public bool Customized;
  public float CenterX = 0.5f;
  public float CenterY = 0.5f;
  public float Scale = 1f;

  // Version zero retains the legacy normalized center and scale interpretation.
  // Only a completed explicit edit writes the anchored format; loading or fitting
  // a viewport must not migrate the persisted placement.
  public int LayoutVersion;
  public Anchor9 Anchor = Anchor9.Center;
  public float OffsetX;
  public float OffsetY;
}
