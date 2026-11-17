using System;
using Element.Configuration;
using Element.Presentation.Settings.Chatter;
using Element.Presentation.Settings.EditorTweaks;
using Element.Presentation.Settings.Effects;
using Element.Presentation.Settings.General;
using Element.Presentation.Settings.Home;
using Element.Presentation.Settings.Judgment;
using Element.Presentation.Settings.Planets;
using Element.Presentation.Settings.PresetEditor;
using Element.Presentation.Settings.Profiles;
using Element.Presentation.Settings.Tweaks;
using ElementUI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Settings.Window;

/// <summary>
/// The settings window: which page shows, Back and Close, and the message
/// when saving failed. Every page is made once, when the window is first
/// built, and kept, so a page keeps its scroll and inputs while you look elsewhere.
/// </summary>
internal sealed class SettingsWindowViewModel : ViewModel
{
  private readonly ISettingsWindowActions _screen;

  public readonly ElementSettings Settings;
  public readonly SettingsFeatures Features;

  public readonly HomeViewModel Home;
  public readonly TweaksViewModel Tweaks;
  public readonly EditorTweaksViewModel EditorTweaks;
  public readonly JudgmentViewModel Judgment;
  public readonly PlanetsViewModel Planets;
  public readonly ChatterViewModel Chatter;
  public readonly EffectsViewModel Effects;
  public readonly PresetEditorViewModel PresetEditor;
  public readonly GeneralViewModel General;

  /// <summary>Null when profiles aren't available.</summary>
  public readonly ProfilesViewModel Profiles;

  /// <summary>The page shown now.</summary>
  public SettingsPageViewModel Current;

  /// <summary>Why the last save failed, or empty.</summary>
  public string SaveError = "";

  /// <summary>The window is under 600 wide: its frame keeps 20 at the sides instead of 32.</summary>
  public bool Compact;

  public Computed<bool> CanGoBack { get; }
  public Computed<bool> SaveFailed { get; }
  public Command Back { get; }
  public Command Close { get; }
  public Command RetrySave { get; }

  public SettingsWindowViewModel(ElementSettings settings, SettingsFeatures features, ISettingsWindowActions screen)
  {
    Settings = settings;
    Features = features;
    _screen = screen;
    Home = Own(new HomeViewModel(this));
    Tweaks = Own(new TweaksViewModel(this, Home));
    EditorTweaks = Own(new EditorTweaksViewModel(this, Tweaks));
    Judgment = Own(new JudgmentViewModel(this, Tweaks));
    Planets = Own(new PlanetsViewModel(this, Tweaks));
    Chatter = Own(new ChatterViewModel(this, Tweaks));
    Effects = Own(new EffectsViewModel(this, Home));
    PresetEditor = Own(new PresetEditorViewModel(this, Effects));
    General = Own(new GeneralViewModel(this, Home));
    if (features.Profiles != null)
      Profiles = Own(new ProfilesViewModel(this, General));
    Pages =
      Profiles != null
        ? new SettingsPageViewModel[]
        {
          Home,
          Tweaks,
          EditorTweaks,
          Judgment,
          Planets,
          Chatter,
          Effects,
          PresetEditor,
          General,
          Profiles,
        }
        : new SettingsPageViewModel[]
        {
          Home,
          Tweaks,
          EditorTweaks,
          Judgment,
          Planets,
          Chatter,
          Effects,
          PresetEditor,
          General,
        };
    Current = Home;
    CanGoBack = Computed(() => Current.Parent != null);
    SaveFailed = Computed(() => SaveError.Length > 0);
    Back = Command(GoBack, canExecute: CanGoBack);
    Close = Command(() => _screen.Close());
    RetrySave = Command(() =>
    {
      if (_screen.Save())
        SaveError = "";
    });
  }

  /// <summary>Every page, for the window's per-frame and open/close calls.</summary>
  public readonly SettingsPageViewModel[] Pages;

  /// <summary>Shows <paramref name="page"/>. Pages call it from their commands.</summary>
  public void Show(SettingsPageViewModel page)
  {
    if (page == null || ReferenceEquals(page, Current))
      return;
    foreach (SettingsPageViewModel each in Pages)
      each.CancelCapture();
    Current = page;
    page.Shown();
  }

  private void GoBack()
  {
    if (Current.Parent != null)
      Show(Current.Parent);
  }

  /// <summary>A control changed a setting: apply it.</summary>
  public void Changed() => _screen.Apply();

  /// <summary>Saves; on failure the window says why and offers to try again.</summary>
  public bool Save()
  {
    if (_screen.Save())
    {
      SaveError = "";
      return true;
    }
    SaveError = L(
      "설정을 저장하지 못했어요. 모드 폴더의 쓰기 권한을 확인하고 다시 닫아주세요.",
      "Settings could not be saved. Check write permission for the mod folder, then close again."
    );
    Runtime.Probe();
    return false;
  }

  /// <summary>The window was sized <paramref name="width"/> wide by the screen it is on.</summary>
  public void Resized(float width)
  {
    if (Compact == width < 600)
      return;
    Compact = width < 600;
    Runtime.Probe();
  }

  /// <summary>Opens the overlay editor, after saving.</summary>
  public void EditLayout()
  {
    if (Save())
      _screen.EditLayout();
  }

  /// <summary>The window opened: start on the first page.</summary>
  public void Opened()
  {
    SaveError = "";
    Current = Home;
    foreach (SettingsPageViewModel page in Pages)
      page.Opened();
    Home.Shown();
    Runtime.Probe();
  }
}
