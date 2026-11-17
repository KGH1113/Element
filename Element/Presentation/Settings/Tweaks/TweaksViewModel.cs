using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Input;
using ElementUI;

namespace Element.Presentation.Settings.Tweaks;

/// <summary>Tweaks: links to the editor, judgment, planet and chatter pages, then record mode and the trackpad.</summary>
internal sealed class TweaksViewModel : SettingsPageViewModel
{
  /// <summary>The trackpad gestures exist only on macOS.</summary>
  public readonly bool HasTrackpad = ShortcutKeys.IsMac;

  public Command OpenEditor { get; }
  public Command OpenJudgment { get; }
  public Command OpenPlanets { get; }
  public Command OpenChatter { get; }

  public TweaksViewModel(SettingsWindowViewModel window, SettingsPageViewModel parent)
    : base(window, parent, "Tweaks")
  {
    OpenEditor = Command(() => window.Show(window.EditorTweaks));
    OpenJudgment = Command(() => window.Show(window.Judgment));
    OpenPlanets = Command(() => window.Show(window.Planets));
    OpenChatter = Command(() => window.Show(window.Chatter));
  }
}
