using ElementUI;

namespace Element.Presentation.Settings.EditorTweaks;

/// <summary>A step angle offered as a button on the editor page: its label and what choosing it does.</summary>
internal sealed class RotationPreset
{
  public readonly string Label;
  public readonly Command Use;

  public RotationPreset(string label, Command use)
  {
    Label = label;
    Use = use;
  }
}
