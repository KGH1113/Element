using System.Xml.Serialization;

namespace Element.Configuration.Gameplay;

[CopiedInPlace]
public sealed class EditorTweaksOptions
{
  public bool ShowAngle;
  public bool ShowBeats;
  public bool ShowCount;
  public bool ShowDuration;
  public bool ExcludeLastInterval;
  public bool FineRotation;
  public float RotationStep = 22.5f;
  public bool SyncEventPositions;

  [XmlIgnore]
  public bool HasSelectionInfo => ShowAngle || ShowBeats || ShowCount || ShowDuration;
}
