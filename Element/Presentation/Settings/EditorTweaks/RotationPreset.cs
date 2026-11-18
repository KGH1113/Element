using ElementUI;

namespace Element.Presentation.Settings.EditorTweaks;

/// <summary>A step angle offered as a button on the editor page: its label and what choosing it does.</summary>
internal sealed class RotationPreset
{
  public readonly string Label;
  public readonly Command Use;

  /// <summary>It is the angle set now.</summary>
  public readonly Computed<bool> Picked;

  public RotationPreset(string label, Command use, Computed<bool> picked)
  {
    Label = label;
    Use = use;
    Picked = picked;
  }
}
