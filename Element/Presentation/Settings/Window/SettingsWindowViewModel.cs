using System;
using Element.Configuration;
using Element.Presentation.Settings.Pages;
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
  private readonly Action _apply;
  private readonly Func<bool> _save;
  private readonly Action _editLayout;
  private readonly Action _close;

  public readonly ElementSettings Settings;
  public readonly SettingsFeatures Features;

  public readonly HomeModel Home;
  public readonly TweaksModel Tweaks;
  public readonly EditorTweaksModel EditorTweaks;
  public readonly JudgmentModel Judgment;
  public readonly PlanetsModel Planets;
  public readonly ChatterModel Chatter;
  public readonly EffectsModel Effects;
  public readonly PresetEditorModel PresetEditor;
  public readonly GeneralModel General;

  /// <summary>Null when profiles aren't available.</summary>
  public readonly ProfilesModel Profiles;

  /// <summary>The page shown now.</summary>
  public SettingsPageModel Current;

  /// <summary>Why the last save failed, or empty.</summary>
  public string SaveError = "";

  /// <summary>The window is under 600 wide: its frame keeps 20 at the sides instead of 32.</summary>
  public bool Compact;

  public Computed<bool> CanGoBack { get; }
  public Computed<bool> SaveFailed { get; }
  public Command Back { get; }
  public Command Close { get; }
  public Command RetrySave { get; }

  public SettingsWindowViewModel(
    ElementSettings settings,
    SettingsFeatures features,
    Action apply,
    Func<bool> save,
    Action editLayout,
    Action close
  )
  {
    Settings = settings;
    Features = features;
    _apply = apply;
    _save = save;
    _editLayout = editLayout;
    _close = close;
    Home = Own(new HomeModel(this));
    Tweaks = Own(new TweaksModel(this, Home));
    EditorTweaks = Own(new EditorTweaksModel(this, Tweaks));
    Judgment = Own(new JudgmentModel(this, Tweaks));
    Planets = Own(new PlanetsModel(this, Tweaks));
    Chatter = Own(new ChatterModel(this, Tweaks));
    Effects = Own(new EffectsModel(this, Home));
    PresetEditor = Own(new PresetEditorModel(this, Effects));
    General = Own(new GeneralModel(this, Home));
    if (features.Profiles != null)
      Profiles = Own(new ProfilesModel(this, General));
    Pages =
      Profiles != null
        ? new SettingsPageModel[] { Home, Tweaks, EditorTweaks, Judgment, Planets, Chatter, Effects, PresetEditor, General, Profiles }
        : new SettingsPageModel[] { Home, Tweaks, EditorTweaks, Judgment, Planets, Chatter, Effects, PresetEditor, General };
    Current = Home;
    CanGoBack = Computed(() => Current.Parent != null);
    SaveFailed = Computed(() => SaveError.Length > 0);
    Back = Command(GoBack, canExecute: CanGoBack);
    Close = Command(() => _close());
    RetrySave = Command(() =>
    {
      if (_save())
        SaveError = "";
    });
  }

  /// <summary>Every page, for the window's per-frame and open/close calls.</summary>
  public readonly SettingsPageModel[] Pages;

  /// <summary>Shows <paramref name="page"/>. Pages call it from their commands.</summary>
  public void Show(SettingsPageModel page)
  {
    if (page == null || ReferenceEquals(page, Current))
      return;
    foreach (SettingsPageModel each in Pages)
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
  public void Changed() => _apply();

  /// <summary>Saves; on failure the window says why and offers to try again.</summary>
  public bool Save()
  {
    if (_save())
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
      _editLayout();
  }

  /// <summary>The window opened: start on the first page.</summary>
  public void Opened()
  {
    SaveError = "";
    Current = Home;
    foreach (SettingsPageModel page in Pages)
      page.Opened();
    Home.Shown();
    Runtime.Probe();
  }
}
