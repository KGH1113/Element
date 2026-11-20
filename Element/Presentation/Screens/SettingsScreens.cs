using System;
using Element.Application.KeyViewer;
using Element.Application.Rendering;
using Element.Configuration;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.OverlayEditor;
using Element.Presentation.Settings;
using Element.Presentation.Settings.Planets;
using Element.Presentation.Settings.Window;
using Element.Presentation.Ui.Input;
using ElementUI.Ugui;
using TMPro;

namespace Element.Presentation.Screens;

/// <summary>
/// The settings window and the layout editor it opens. The settings shortcut
/// opens the window, or closes the editor and saves; the open one runs each
/// frame. While either is open the game's input waits and ElementNote's native
/// viewers make way. The window, about 500 objects, is built the first time
/// it opens. A render records them with the game, so neither opens during
/// one, and one already open closes when the render starts.
/// </summary>
internal sealed class SettingsScreens : IDisposable
{
  private readonly ElementSettings _settings;
  private readonly TMP_FontAsset _font;
  private readonly INativeViewerLayer _nativeViewers;
  private readonly PlanetPreviews _planetPreviews;
  private readonly Action _apply;
  private readonly Func<bool> _save;
  private readonly Action _viewerModeChanged;
  private readonly Func<SettingsFeatures> _features;
  private readonly IRenderVideo _video;
  private OverlayEditorScreen _layoutEditor;
  private SettingsWindowScreen _panel;

  /// <param name="apply">Applies the settings to the game after a change.</param>
  /// <param name="save">Writes the settings; false when that failed.</param>
  /// <param name="viewerModeChanged">The layout editor opened or closed: the key viewers show as it needs.</param>
  /// <param name="features">What the window needs beyond the settings, made when it is built.</param>
  /// <param name="video">The render that would record the window and the editor.</param>
  public SettingsScreens(
    ElementSettings settings,
    TMP_FontAsset font,
    OverlayCanvasHost canvas,
    IKeyViewerElements keyViewers,
    INativeViewerLayer nativeViewers,
    PlanetPreviews planetPreviews,
    Action apply,
    Func<bool> save,
    Action viewerModeChanged,
    Func<SettingsFeatures> features,
    IRenderVideo video
  )
  {
    _settings = settings;
    _font = font;
    _nativeViewers = nativeViewers;
    _planetPreviews = planetPreviews;
    _apply = apply;
    _save = save;
    _viewerModeChanged = viewerModeChanged;
    _features = features;
    _video = video ?? throw new ArgumentNullException(nameof(video));
    _layoutEditor = new OverlayEditorScreen(canvas, keyViewers, font, OnLayoutClosed, settings);
  }

  public bool IsEditingLayout => _layoutEditor?.IsOpen == true;
  public bool IsOpen => IsEditingLayout || _panel?.IsOpen == true;

  private SettingsWindowScreen Panel =>
    _panel ??= new SettingsWindowScreen(_settings, _font, _apply, _save, OpenLayout, OnVisibilityChanged, _features());

  public void Open()
  {
    if (!_video.IsRendering)
      Panel.Open();
  }

  /// <summary>The effect presets changed outside the window; its gallery shows them again.</summary>
  public void RefreshPresets() => _panel?.RefreshPresets();

  /// <summary>Runs every frame: the settings shortcut and the open editor or window.</summary>
  public void Tick()
  {
    if (_video.IsRendering)
      CloseForRender();
    else if (_layoutEditor?.IsOpen == true)
    {
      if (SettingsWindowScreen.ShortcutPressed(_settings))
        _layoutEditor.Close(true);
      else
        _layoutEditor.Tick();
      if (_layoutEditor.IsOpen)
        _nativeViewers.SetEditorExclusions(_layoutEditor.NativeExclusions);
      UiInputCapture.RestoreCursor();
    }
    else if (_panel != null)
      _panel.Tick();
    else if (SettingsWindowScreen.ShortcutPressed(_settings) && !Keyboard.IsTaken)
      Open();
  }

  // Keeps what was changed. Only a value typed wrong in the editor keeps it open.
  private void CloseForRender()
  {
    if (_layoutEditor?.IsOpen == true)
      _layoutEditor.Close(true);
    _panel?.CloseForRender();
  }

  private void OnVisibilityChanged(bool visible)
  {
    if (visible)
      UiInputCapture.Begin();
    else
    {
      UiInputCapture.End();
      _planetPreviews.Release();
    }
    _nativeViewers.SetVisible(!visible);
  }

  private void OpenLayout()
  {
    UiInputCapture.Begin(layoutEditing: true);
    _layoutEditor.Open();
    _nativeViewers.SetEditorExclusions(_layoutEditor.NativeExclusions);
    _viewerModeChanged();
  }

  private void OnLayoutClosed(bool save)
  {
    bool saved = !save || _save();
    _viewerModeChanged();
    // Closed for a render: the window stays closed and the game gets its input back.
    if (_video.IsRendering)
    {
      UiInputCapture.End();
      return;
    }
    Panel.Open();
    if (!saved)
      Panel.ShowSaveError();
  }

  public void Dispose()
  {
    _layoutEditor?.Dispose();
    _layoutEditor = null;
    _panel?.Dispose();
    _panel = null;
    _planetPreviews.Release();
    UiInputCapture.Reset();
  }
}
