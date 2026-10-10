using UnityModManagerNet;

namespace ImplResourcePack.Configuration;

public sealed class ImplResourcePackSettings : UnityModManager.ModSettings
{
  public bool HidePerfectJudgmentText = true;

  public bool RecordMode;

  public bool NativeEditorTrackpad = true;

  public bool ShowDmNoteInGame = true;

  public override void Save(UnityModManager.ModEntry modEntry)
  {
    Save(this, modEntry);
  }
}
