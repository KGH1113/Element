using System;

namespace Element.Configuration;

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

  // One of OverlayElementKinds. Settings saved before 1.1 name a built-in
  // overlay here (or leave it empty); loading converts those to text.
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

  // A text converted from a built-in overlay sits where that overlay did until
  // it is moved. Empty for elements added in the editor.
  public string DefaultPlacement = string.Empty;

  // The combo's title and number were one element. Both halves keep the old
  // placement here until they are fitted together on their first layout.
  public string PendingGroup = string.Empty;

  public OverlayTextSettings Text;
  public OverlayShapeSettings Shape;

  public OverlayLayoutSettings Clone()
  {
    OverlayLayoutSettings copy = new();
    copy.CopyFrom(this);
    return copy;
  }

  public void CopyFrom(OverlayLayoutSettings source)
  {
    Id = source.Id;
    TemplateId = source.TemplateId;
    Enabled = source.Enabled;
    CopyPlacementFrom(source);
    DefaultPlacement = source.DefaultPlacement;
    PendingGroup = source.PendingGroup;
    Text = source.Text?.Clone();
    Shape = source.Shape?.Clone();
  }

  public void CopyPlacementFrom(OverlayLayoutSettings source)
  {
    Customized = source.Customized;
    CenterX = source.CenterX;
    CenterY = source.CenterY;
    Scale = source.Scale;
    LayoutVersion = source.LayoutVersion;
    Anchor = source.Anchor;
    OffsetX = source.OffsetX;
    OffsetY = source.OffsetY;
  }

  public bool SameAs(OverlayLayoutSettings other) =>
    other != null
    && Id == other.Id
    && TemplateId == other.TemplateId
    && Enabled == other.Enabled
    && Customized == other.Customized
    && CenterX == other.CenterX
    && CenterY == other.CenterY
    && Scale == other.Scale
    && LayoutVersion == other.LayoutVersion
    && Anchor == other.Anchor
    && OffsetX == other.OffsetX
    && OffsetY == other.OffsetY
    && DefaultPlacement == other.DefaultPlacement
    && PendingGroup == other.PendingGroup
    && (Text?.SameAs(other.Text) ?? other.Text == null)
    && (Shape?.SameAs(other.Shape) ?? other.Shape == null);
}
