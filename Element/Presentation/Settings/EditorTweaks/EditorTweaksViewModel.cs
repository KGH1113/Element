using System.Globalization;
using Element.Configuration.Gameplay;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Input;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.EditorTweaks;

/// <summary>Tweaks → Editor: what the selection shows, and fine rotation with event positions.</summary>
internal sealed class EditorTweaksViewModel : SettingsPageViewModel
{
  /// <summary>The fine rotation shortcut, as this platform writes it.</summary>
  public readonly string FineRotationDetail =
    (ShortcutKeys.IsMac ? "Cmd + Option" : "Ctrl + Alt")
    + L(" + 쉼표·마침표로 선택한 각도만큼 회전합니다.", " + comma or period rotates by your chosen step.");

  /// <summary>The step buttons beside the typed angle.</summary>
  public readonly RotationPreset[] Presets;

  /// <summary>The angle in the field: what was typed, until it is kept.</summary>
  public string RotationText;

  /// <summary>The typed angle that couldn't be kept; the error shows until the field changes.</summary>
  public string RejectedRotation;

  /// <summary>Leaving out the last interval only changes angle, beats and time.</summary>
  public Computed<bool> CanExcludeLastInterval { get; }

  public Computed<string> RotationError { get; }

  /// <summary>Keeps the typed angle when typing ends, or says why it can't.</summary>
  public Command CommitRotation { get; }

  public EditorTweaksViewModel(SettingsWindowViewModel window, SettingsPageViewModel parent)
    : base(window, parent, L("에디터", "Editor tweaks"))
  {
    RotationText = Format(Settings.Gameplay.Editor.RotationStep);
    CanExcludeLastInterval = Computed(() =>
      Settings.Gameplay.Editor.ShowAngle || Settings.Gameplay.Editor.ShowBeats || Settings.Gameplay.Editor.ShowDuration
    );
    RotationError = Computed(() =>
      RejectedRotation != null && RotationText == RejectedRotation
        ? L("0.1°부터 180° 사이의 각도를 입력하세요.", "Enter an angle between 0.1° and 180°.")
        : ""
    );
    CommitRotation = Command(KeepTypedRotation);
    Presets = new[] { Preset(22.5f), Preset(15), Preset(7.5f), Preset(1) };
  }

  private RotationPreset Preset(float step) =>
    new(
      Format(step) + "°",
      Command(() =>
      {
        Settings.Gameplay.Editor.RotationStep = step;
        ShowRotationStep();
      }),
      Computed(() => Settings.Gameplay.Editor.RotationStep == step)
    );

  // Runs when the field loses focus or Enter is pressed. The field's own write
  // already told the window; a kept angle changes a setting after it, so this
  // tells the window again.
  private void KeepTypedRotation()
  {
    if (
      !float.TryParse(RotationText, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
      || float.IsNaN(value)
      || value < .1f
      || value > 180
    )
    {
      RejectedRotation = RotationText;
      return;
    }
    RejectedRotation = null;
    EditorTweaksOptions editor = Settings.Gameplay.Editor;
    if (editor.RotationStep == value)
      return;
    editor.RotationStep = value;
    Window.Changed();
  }

  private void ShowRotationStep()
  {
    RotationText = Format(Settings.Gameplay.Editor.RotationStep);
    RejectedRotation = null;
  }

  public override void Opened() => ShowRotationStep();

  public override void SettingsReplaced() => ShowRotationStep();

  private static string Format(float step) => step.ToString(CultureInfo.InvariantCulture);
}
