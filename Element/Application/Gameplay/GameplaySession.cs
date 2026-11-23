using System;
using Element.Configuration;

namespace Element.Application.Gameplay;

/// <summary>
/// When the overlay and the in-game key viewers show, and what applying settings
/// redoes. Record mode and co-op keep both hidden; the layout editor shows the key
/// viewers outside a play too. Between plays, in co-op and in record mode the key
/// viewers wait loaded and hidden.
/// </summary>
internal sealed class GameplaySession
{
  private readonly Func<ElementSettings> _settings;
  private readonly IPlayState _play;
  private readonly IGameplayOverlay _overlay;
  private readonly IInGameKeyViewers _viewers;
  private readonly IRecordMode _recording;
  private readonly IElementNoteApp _app;
  private readonly Func<bool> _editingLayout;
  private readonly Action _applyLook;
  private readonly Action _applyLayouts;
  private bool _layoutsApplied;
  private bool _appAutoOpen;
  private bool _appChecked;

  /// <param name="editingLayout">The layout editor is open.</param>
  /// <param name="applyLook">Applies settings cheap enough for every sample of a color drag.</param>
  /// <param name="applyLayouts">Rebuilds the overlay's and the key viewers' layouts.</param>
  public GameplaySession(
    Func<ElementSettings> settings,
    IPlayState play,
    IGameplayOverlay overlay,
    IInGameKeyViewers viewers,
    IRecordMode recording,
    IElementNoteApp app,
    Func<bool> editingLayout,
    Action applyLook,
    Action applyLayouts
  )
  {
    _settings = settings;
    _play = play;
    _overlay = overlay;
    _viewers = viewers;
    _recording = recording;
    _app = app;
    _editingLayout = editingLayout;
    _applyLook = applyLook;
    _applyLayouts = applyLayouts;
  }

  public bool IsRecording => _recording.IsActive;

  /// <summary>Whether a play shows the key viewers, as a video of it should.</summary>
  public bool ShowsKeyViewersInPlay => !IsRecording && !_play.IsCoop;

  public void Show()
  {
    if (IsRecording)
    {
      Hide();
      _recording.ReapplyCurrentScene();
      return;
    }
    if (_play.IsCoop)
    {
      Hide();
      return;
    }

    RefreshViewers(true);
    _overlay.Show();
  }

  public void Hide()
  {
    RefreshViewers(false);
    _overlay.Hide();
  }

  /// <summary>The key viewers follow the play going on now, or the layout editor.</summary>
  public void RefreshViewers() => RefreshViewers(_play.IsInGameWorld);

  // The pages stay loaded between plays, in co-op and in record mode, so starting
  // or restarting a play, or turning record mode off, only shows them.
  private void RefreshViewers(bool gameplay)
  {
    bool editing = _editingLayout();
    bool ready = _viewers.Supported && _viewers.HasEnabledViewers;
    bool shown = editing || (gameplay && ShowsKeyViewersInPlay);
    _viewers.SetPresence(
      !ready ? KeyViewerPresence.Off
      : shown ? KeyViewerPresence.Shown
      : KeyViewerPresence.Ready
    );
  }

  public void ApplySettings()
  {
    ElementSettings settings = _settings();
    SyncApp(settings.AutoOpenCloseDmNote);
    _applyLook();
    // Color drags only update the affected planet. Avoid rebuilding overlay
    // layouts, refreshing all counters or scanning the recording HUD per sample.
    if (_layoutsApplied && IsRecording == settings.RecordMode)
      return;
    _layoutsApplied = true;
    _applyLayouts();
    _recording.Apply(settings.RecordMode);
    if (IsRecording)
    {
      Hide();
      return;
    }

    if (_play.IsInGameWorld)
      Show();
    else
      RefreshViewers(false);
  }

  /// <summary>The live settings got new layouts, as from a profile: the next apply builds them.</summary>
  public void LayoutsReplaced() => _layoutsApplied = false;

  // Installs or updates the app once at startup, so players can open it on its
  // own even with auto open off. Opens it then and again when the option is
  // switched on; settings changes such as color drags do not start the app check again.
  private void SyncApp(bool autoOpen)
  {
    if (autoOpen && !_appAutoOpen)
      _app?.OpenIfClosed();
    else if (!_appChecked)
      _app?.InstallIfClosed();
    _appChecked = true;
    _appAutoOpen = autoOpen;
  }
}
