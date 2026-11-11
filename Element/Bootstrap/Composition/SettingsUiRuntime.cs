using System;
using Element.Application.Input;
using Element.Application.Profiles;
using Element.Configuration.Effects;
using Element.Configuration.Gameplay;
using Element.Infrastructure.ElementNote.Viewers;
using Element.Infrastructure.Game.Effects;
using Element.Infrastructure.Game.Input;
using Element.Infrastructure.Game.Planets;
using Element.Infrastructure.Game.Play;
using Element.Infrastructure.Platform;
using Element.Infrastructure.Storage;
using Element.Infrastructure.TufReplay;
using Element.Presentation.Layout;
using Element.Presentation.Layout.KeyViewers;
using Element.Presentation.Notices;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Fonts;
using Element.Presentation.Screens;
using Element.Presentation.Settings;
using Element.Presentation.Settings.Pages;
using Element.Presentation.Ui.Input;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;

namespace Element.Bootstrap.Composition;

/// <summary>
/// Builds and connects the settings window, the layout editor, the planet
/// preview they use and the effect preset shortcuts. Nothing here runs during
/// gameplay except a shortcut check per frame.
/// </summary>
internal sealed class SettingsUiRuntime : IDisposable
{
  private readonly Main _main;
  private readonly OverlayCanvasHost _canvas;
  private readonly Action _reloadSettings;
  private readonly Func<bool> _judgementTextLinked;
  private readonly TufReplayInputPause _tufReplayInput;
  private readonly PlanetPreviews _planetPreviews;
  private readonly SettingsScreens _screens;
  private readonly UiToast _toast;
  private readonly EffectPresetShortcuts _presetShortcuts;

  public SettingsUiRuntime(
    Main main,
    OverlayCanvasHost canvas,
    TMP_FontAsset overlayFont,
    ElementNoteNativeViewers nativeViewers,
    PlanetAppearanceController planets,
    Action apply,
    Action reloadSettings,
    Action viewerModeChanged,
    Func<bool> judgementTextLinked,
    Func<bool> playing
  )
  {
    _main = main;
    _canvas = canvas;
    _judgementTextLinked = judgementTextLinked;
    _reloadSettings = reloadSettings;
    _tufReplayInput = new TufReplayInputPause(main.LogWarning, main.LogException);
    TMP_FontAsset font = UiFonts.Choose(overlayFont);
    nativeViewers.ConfigurePreview(new NativeViewerSamples(font));
    _planetPreviews = new PlanetPreviews(() => new PlanetPreviewRenderer(planets), main.LogException);
    _screens = new SettingsScreens(
      main.Settings,
      font,
      canvas,
      nativeViewers,
      nativeViewers,
      _planetPreviews,
      apply,
      Save,
      viewerModeChanged,
      CreateFeatures
    );
    _toast = new UiToast(font, () => main.Settings.ReduceUiMotion);
    _presetShortcuts = new EffectPresetShortcuts(
      main.Settings,
      playing,
      () => _screens.IsOpen || SettingsPanel.IsTyping(),
      apply,
      _screens.RefreshPresets,
      Save,
      _toast
    );
  }

  public bool IsEditingLayout => _screens.IsEditingLayout;
  public bool IsOpen => _screens.IsOpen;

  private SettingsFeatures CreateFeatures() =>
    new()
    {
      RenderPlanetPreview = _planetPreviews.Render,
      BlockedChatter = () => _main.Chatter.Blocked,
      Updates = _main.Updates,
      JudgementTextLinked = _judgementTextLinked,
      Profiles = CreateProfiles(),
      OverlayFonts = () => _canvas?.Fonts,
    };

  // Built with the window, after the game has loaded its language: the first
  // profile is named in it.
  private IProfiles CreateProfiles()
  {
    try
    {
      return new ProfileService(
        new ProfileLibrary(_main.Settings, ProfileNames.Default),
        SystemFileDialogs.Create(_main.RuntimePath),
        new ProfileFileStore(),
        Save,
        _reloadSettings,
        new SettingsBackup(_main.Path, () => _main.Settings).Write,
        _main.Version,
        _main.LogException
      );
    }
    catch (Exception exception)
    {
      _main.LogException("Profiles are unavailable", exception);
      return null;
    }
  }

  public void Open() => _screens.Open();

  /// <summary>Runs every frame: the open editor or window, then the preset shortcuts.</summary>
  public void Tick()
  {
    _screens.Tick();
    _presetShortcuts.Tick();
    // Exactly when the game stops getting keys: the layout editor lets them through
    // during play, and TUFReplay must keep recording keys the game judges.
    _tufReplayInput.Set(InputGate.BlocksAsyncKeyboard);
  }

  public bool Save()
  {
    try
    {
      _main.Settings.Save(_main.ModEntry);
      return true;
    }
    catch (Exception exception)
    {
      _main.LogException("Save settings", exception);
      return false;
    }
  }

  /// <summary>A short notice over the game, such as why a render lacks the key viewers.</summary>
  public void ShowNotice(string title, string detail) => _toast.Show(title, detail);

  public void Dispose()
  {
    _screens.Dispose();
    _toast.Dispose();
    _tufReplayInput.Dispose();
  }
}
