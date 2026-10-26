using System;
using ADOFAI;
using ImplResourcePack.Configuration.Effects;

namespace ImplResourcePack.Infrastructure.Game.Effects;

internal sealed class EffectsController : IDisposable
{
  private readonly Func<EffectSettings> _settings;
  private RemovedEffects _requested;
  private RemovedEffects _applied;
  private int _renderDepth;
  private int _editorReturnDepth;
  private bool _disposed;

  internal static EffectsController Active { get; private set; }
  internal static RemovedEffects Applied => Active?._applied ?? RemovedEffects.None;
  internal static bool Rendering => (Active?._renderDepth ?? 0) > 0;

  // Keep one visual preset through loading, editing, playback and editor return.
  // Temporarily restoring originals on return would reload every removed image.
  internal static RemovedEffects ForRendering => Applied;
  public bool RequiresRestart => _requested != _applied;
  internal bool ReturningToEditor => _editorReturnDepth > 0;

  public EffectsController(Func<EffectSettings> settings)
  {
    _settings = settings;
    ApplySettings();
    _applied = _requested;
    Active = this;
  }

  public void ApplySettings()
  {
    if (_disposed)
      return;
    _requested = (_settings()?.ActiveEffects ?? RemovedEffects.None) & EffectCatalog.All;
  }

  internal bool CommitForPlayback()
  {
    // SwitchToEditMode also calls ResetScene. Keep the current visual preset
    // instead of committing pending settings and rebuilding assets on return.
    if (_disposed || ReturningToEditor)
      return false;
    ApplySettings();
    bool changed = _applied != _requested;
    _applied = _requested;
    return changed;
  }

  internal IDisposable BeginRender(scnGame game)
  {
    RemovedEffects effects = ForRendering;
    if (_disposed || game == null || game.levelData == null || effects == RemovedEffects.None || _renderDepth > 0)
      return null;
    LevelData original = game.levelData;
    LevelData rendered = EffectLevelFilter.Create(original, effects);
    _renderDepth++;
    game.levelData = rendered;
    return new RenderScope(this, game, original, rendered);
  }

  internal IDisposable BeginEditorReturn()
  {
    _editorReturnDepth++;
    return new EditorReturnScope(this);
  }

  public void Dispose()
  {
    _disposed = true;
    _requested = _applied = RemovedEffects.None;
    if (ReferenceEquals(Active, this))
      Active = null;
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
