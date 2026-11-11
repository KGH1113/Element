using System;
using System.Collections.Generic;
using ADOFAI;
using Element.Configuration.Effects;

namespace Element.Infrastructure.Game.Effects;

internal sealed class EffectsController : IDisposable
{
  private readonly Func<EffectSettings> _settings;
  private RemovedEffects _requested;
  private RemovedEffects _applied;
  private int _requestedZoom = EffectPreset.DefaultCameraZoom;
  private int _appliedZoom = EffectPreset.DefaultCameraZoom;
  private int _renderDepth;
  private int _editorReturnDepth;
  private bool _disposed;

  internal RemovedEffects Applied => _applied;
  internal bool Rendering => _renderDepth > 0;

  // Keep one visual preset through loading, editing, playback and editor return.
  // Temporarily restoring originals on return would reload every removed image.
  internal RemovedEffects ForRendering => Applied;
  internal int CameraZoomForRendering => _appliedZoom;
  public bool RequiresRestart => PendingChange;

  // The zoom only reaches the level through the camera reset, so it alone
  // matters only while camera movement is removed.
  private bool PendingChange =>
    _requested != _applied || ((_requested & RemovedEffects.CameraMotion) != 0 && _requestedZoom != _appliedZoom);
  internal bool ReturningToEditor => _editorReturnDepth > 0;

  public EffectsController(Func<EffectSettings> settings)
  {
    _settings = settings;
    ApplySettings();
    _applied = _requested;
    _appliedZoom = _requestedZoom;
  }

  public void ApplySettings()
  {
    if (_disposed)
      return;
    EffectSettings settings = _settings();
    _requested = (settings?.ActiveEffects ?? RemovedEffects.None) & EffectCatalog.All;
    _requestedZoom = settings?.ActiveCameraZoom ?? EffectPreset.DefaultCameraZoom;
  }

  internal bool CommitForPlayback()
  {
    // SwitchToEditMode also calls ResetScene. Keep the current visual preset
    // instead of committing pending settings and rebuilding assets on return.
    if (_disposed || ReturningToEditor)
      return false;
    ApplySettings();
    bool changed = PendingChange;
    _applied = _requested;
    _appliedZoom = _requestedZoom;
    return changed;
  }

  internal IDisposable BeginRender(scnGame game)
  {
    RemovedEffects effects = ForRendering;
    if (_disposed || game == null || game.levelData == null || effects == RemovedEffects.None || _renderDepth > 0)
      return null;
    LevelData original = game.levelData;
    LevelData rendered = EffectLevelFilter.Create(original, effects, _appliedZoom);
    _renderDepth++;
    game.levelData = rendered;
    return new RenderScope(this, game, original, rendered);
  }

  internal IDisposable BeginEditorReturn()
  {
    _editorReturnDepth++;
    return new EditorReturnScope(this);
  }

  /// <summary>
  /// A retry resets the level: applies the pending preset and renders filtered. The
  /// scope goes into <paramref name="scope"/> first, so a failure after it still ends it.
  /// </summary>
  internal void BeforeReset(scnGame game, ref bool isResetCustomLevel, out IDisposable scope)
  {
    bool changed = CommitForPlayback();
    scope = BeginRender(game);
    if (!changed)
      return;
    // A full regeneration restores effects removed by the previous preset, too.
    isResetCustomLevel = true;
    game.UpdateDecorationObjects();
    game.UpdateVideo();
  }

  /// <summary>A level starts playing; like <see cref="BeforeReset"/>, with the floors remade.</summary>
  internal void BeforePlay(scnGame game, ref bool remakeFloors, out IDisposable scope)
  {
    bool changed = CommitForPlayback();
    scope = BeginRender(game);
    if (!changed)
      return;
    remakeFloors = true;
    game.RemakePath();
    game.UpdateDecorationObjects();
    game.SetBackground();
    game.UpdateVideo();
  }

  /// <summary>The level data floors are built from, filtered unless a render scope already did.</summary>
  internal void FilterFloorInput(ref LevelData levelData, ref List<LevelEvent> events)
  {
    RemovedEffects effects = ForRendering;
    if (Rendering || effects == RemovedEffects.None)
      return;
    levelData = EffectLevelFilter.Create(levelData, effects, CameraZoomForRendering, events);
    events = levelData.levelEvents;
  }

  /// <summary>Filters only the decoration list's display entries; saving and undo still use originals.</summary>
  internal void FilterEditorDecorations(List<LevelEvent> shown) =>
    EffectLevelFilter.FilterEditorDecorations(shown, ADOBase.editor?.levelData, ForRendering);

  public void Dispose()
  {
    _disposed = true;
    _requested = _applied = RemovedEffects.None;
  }

  private sealed class RenderScope : IDisposable
  {
    private EffectsController _owner;
    private readonly scnGame _game;
    private readonly LevelData _original;
    private readonly LevelData _rendered;

    public RenderScope(EffectsController owner, scnGame game, LevelData original, LevelData rendered)
    {
      _owner = owner;
      _game = game;
      _original = original;
      _rendered = rendered;
    }

    public void Dispose()
    {
      if (_owner == null)
        return;
      if (_game != null && ReferenceEquals(_game.levelData, _rendered))
        _game.levelData = _original;
      _owner._renderDepth--;
      _owner = null;
    }
  }

  private sealed class EditorReturnScope : IDisposable
  {
    private EffectsController _owner;

    public EditorReturnScope(EffectsController owner) => _owner = owner;

    public void Dispose()
    {
      if (_owner == null)
        return;
      _owner._editorReturnDepth--;
      _owner = null;
    }
  }
}
