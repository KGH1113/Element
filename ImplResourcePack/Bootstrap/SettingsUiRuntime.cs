using System;
using ImplResourcePack.Configuration;
using ImplResourcePack.Infrastructure.Game.Input;
using ImplResourcePack.Infrastructure.Game.Tweaks;
using ImplResourcePack.Infrastructure.Ipc;
using ImplResourcePack.Presentation.Layout;
using ImplResourcePack.Presentation.Overlay.Shared;
using ImplResourcePack.Presentation.Settings;
using TMPro;
using UnityEngine;

namespace ImplResourcePack.Bootstrap;

/// <summary>
/// The settings window, the overlay layout editor and the planet preview they use.
/// Nothing here runs during gameplay except a shortcut check per frame.
/// </summary>
internal sealed class SettingsUiRuntime : IDisposable
{
  private readonly Main _main;
  private readonly TMP_FontAsset _font;
  private readonly Action _apply;
  private readonly Action _viewerModeChanged;
  private readonly DmNoteNativeIpcFeature _nativeViewers;
  private readonly PlanetAppearanceController _planets;
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
    Action viewerModeChanged
  )
  {
    _main = main;
    _nativeViewers = nativeViewers;
    _planets = planets;
    _apply = apply;
    _viewerModeChanged = viewerModeChanged;
    _font =
      overlayFont
      ?? (
        RDString.language == SystemLanguage.Korean ? RDConstants.data.koreanFontTMPro : RDConstants.data.latinFontTMPro
      )
      ?? TMP_Settings.defaultFontAsset;
    // Normalize eagerly so runtime features never see legacy values, even
    // before the lazily built settings window first opens.
    main.Settings.Gameplay.Normalize();
    main.Settings.Effects.Normalize();
    nativeViewers.ConfigurePreview(_font);
    _layoutEditor = new OverlayLayoutEditor(
      canvas,
      nativeViewers.LayoutTargets,
      _font,
      _font != null ? _font.material : null,
      OnLayoutClosed,
      main.Settings,
      nativeViewers.AddElement,
      nativeViewers.GalleryTargets
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
      RenderPlanetPreview,
      () => ChatterBlocker.Blocked
    );

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
        _nativeViewers.SetEditorExclusion(_layoutEditor.NativeExclusionBounds);
      UIInteractionState.RestoreCursor();
    }
    else if (_panel != null)
      _panel.Tick();
    else if (SettingsPanel.ShortcutPressed(_main.Settings) && !SettingsPanel.IsTyping())
      Open();
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
      UIInteractionState.Begin();
    else
    {
      UIInteractionState.End();
      // The preview target and its cloned planet visuals are only needed while
      // settings are open; the planet page recreates them on demand.
      ReleasePlanetPreview();
    }
    _nativeViewers.SetVisible(!visible);
  }

  private void OpenLayout()
  {
    UIInteractionState.Begin(layoutEditing: true);
    _layoutEditor.Open();
    _nativeViewers.SetEditorExclusion(_layoutEditor.NativeExclusionBounds);
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
    UIInteractionState.Reset();
  }
}
