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
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;

namespace Element.Bootstrap.Composition;

/// <summary>
/// The settings window, the overlay layout editor, the planet preview they use
/// and the effect preset shortcuts. Nothing here runs during gameplay except a
/// shortcut check per frame.
/// </summary>
internal sealed class SettingsUiRuntime : IDisposable
{
  private readonly Main _main;
  private readonly OverlayCanvasHost _canvas;
  private readonly TMP_FontAsset _font;
  private readonly Action _apply;
  private readonly Action _reloadSettings;
  private readonly Action _viewerModeChanged;
  private readonly DmNoteNativeIpcFeature _nativeViewers;
  private readonly PlanetAppearanceController _planets;
  private readonly TufReplayInputPause _tufReplayInput;
  private readonly Func<bool> _judgementTextLinked;
  private readonly EffectPresetShortcuts _presetShortcuts;
  private readonly SettingsToast _toast;
  private OverlayLayoutEditor _layoutEditor;
  private SettingsPanel _panel;
  private PlanetPreviewRenderer _planetPreview;
  private bool _planetPreviewFailed;

  public SettingsUiRuntime(
    Main main,
    OverlayCanvasHost canvas,
    TMP_FontAsset overlayFont,
    DmNoteNativeIpcFeature nativeViewers,
    PlanetAppearanceController planets,
    Action apply,
    Action reloadSettings,
    Action viewerModeChanged,
    Func<bool> judgementTextLinked,
    Func<bool> playing
  )
  {
    _main = main;
    _judgementTextLinked = judgementTextLinked;
    _nativeViewers = nativeViewers;
    _planets = planets;
    _tufReplayInput = new TufReplayInputPause(main.LogWarning, main.LogException);
    _apply = apply;
    _reloadSettings = reloadSettings;
    _viewerModeChanged = viewerModeChanged;
    _font = SettingsFonts.Choose(overlayFont);
    nativeViewers.ConfigurePreview(new NativeViewerSamples(_font));
    _canvas = canvas;
    _layoutEditor = new OverlayLayoutEditor(canvas, nativeViewers, _font, OnLayoutClosed, main.Settings);
    _toast = new SettingsToast(_font, () => main.Settings.ReduceUiMotion);
    _presetShortcuts = new EffectPresetShortcuts(
      () => main.Settings.Effects,
      playing,
      () => IsOpen || SettingsPanel.IsTyping(),
      ApplyPresetShortcut
    );
  }

  public bool IsEditingLayout => _layoutEditor?.IsOpen == true;
  public bool IsOpen => IsEditingLayout || _panel?.IsOpen == true;

  // About 500 objects; build the window the first time it opens.
  private SettingsPanel Panel =>
    _panel ??= new SettingsPanel(
      _main.Settings,
      _font,
      _apply,
      Save,
      OpenLayout,
      OnVisibilityChanged,
      new SettingsFeatures
      {
        RenderPlanetPreview = RenderPlanetPreview,
        BlockedChatter = () => _main.Chatter.Blocked,
        Updates = _main.Updates,
        JudgementTextLinked = _judgementTextLinked,
        Profiles = CreateProfiles(),
        OverlayFonts = () => _canvas?.Fonts,
      }
    );

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

  public void Open() => Panel.Open();

  /// <summary>Runs every frame: shortcut handling and the open editor/window.</summary>
  public void Tick()
  {
    if (_layoutEditor?.IsOpen == true)
    {
      if (SettingsPanel.ShortcutPressed(_main.Settings))
        _layoutEditor.Close(true);
      else
        _layoutEditor.Tick();
      if (_layoutEditor.IsOpen)
        _nativeViewers.SetEditorExclusions(_layoutEditor.NativeExclusions);
      SettingsInputCapture.RestoreCursor();
    }
    else if (_panel != null)
      _panel.Tick();
    else if (SettingsPanel.ShortcutPressed(_main.Settings) && !SettingsPanel.IsTyping())
      Open();
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

  private void ApplyPresetShortcut(EffectPreset preset)
  {
    EffectSettings effects = _main.Settings.Effects;
    if (!effects.Toggle(preset.Id))
      return;
    _apply();
    _panel?.RefreshPresets();
    bool saved = Save();
    (string title, string detail) = EffectPresetShortcutNotice.For(preset, effects.Enabled, saved);
    // Record mode hides mod overlays; only a failed save still needs to be seen.
    if (!_main.Settings.RecordMode || !saved)
      _toast.Show(title, detail);
  }

  private Texture RenderPlanetPreview(int index, PlanetAppearanceProfile profile)
  {
    if (_planetPreviewFailed)
      return null;
    try
    {
      _planetPreview ??= new PlanetPreviewRenderer(_planets);
      return _planetPreview.Render(index, profile);
    }
    catch (Exception exception)
    {
      _planetPreviewFailed = true;
      ReleasePlanetPreview();
      _main.LogException("Render planet preview", exception);
      return null;
    }
  }

  private void OnVisibilityChanged(bool visible)
  {
    if (visible)
      SettingsInputCapture.Begin();
    else
    {
      SettingsInputCapture.End();
      // The preview target and its cloned planet visuals are only needed while
      // settings are open; the planet page recreates them on demand.
      ReleasePlanetPreview();
    }
    _nativeViewers.SetVisible(!visible);
  }

  private void OpenLayout()
  {
    SettingsInputCapture.Begin(layoutEditing: true);
    _layoutEditor.Open();
    _nativeViewers.SetEditorExclusions(_layoutEditor.NativeExclusions);
    _viewerModeChanged();
  }

  private void OnLayoutClosed(bool save)
  {
    bool saved = !save || Save();
    _viewerModeChanged();
    Panel.Open();
    if (!saved)
      Panel.ShowSaveError();
  }

  private void ReleasePlanetPreview()
  {
    _planetPreview?.Dispose();
    _planetPreview = null;
  }

  public void Dispose()
  {
    _layoutEditor?.Dispose();
    _layoutEditor = null;
    _panel?.Dispose();
    _panel = null;
    ReleasePlanetPreview();
    _toast.Dispose();
    SettingsInputCapture.Reset();
    _tufReplayInput.Dispose();
  }
}
