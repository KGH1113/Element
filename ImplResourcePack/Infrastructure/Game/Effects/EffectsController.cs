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
  private int _suspended;
  private bool _disposed;

  internal static EffectsController Active { get; private set; }
  internal static RemovedEffects Applied => Active?._applied ?? RemovedEffects.None;
  internal static bool Rendering => (Active?._renderDepth ?? 0) > 0;
  internal static RemovedEffects ForRendering
  {
    get
    {
      EffectsController controller = Active;
      if (controller == null || controller._suspended > 0)
        return RemovedEffects.None;
      scnEditor editor = ADOBase.editor;
      return AllowsRendering(editor != null && editor.inStrictlyEditingMode, controller._renderDepth > 0, false)
        ? controller._applied
        : RemovedEffects.None;
    }
  }
  public bool RequiresRestart => _requested != _applied;

  internal static bool AllowsRendering(bool editing, bool startingPlayback, bool suspended) =>
    !suspended && (!editing || startingPlayback);

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
    ApplySettings();
    bool changed = _applied != _requested;
    _applied = _requested;
    return changed;
  }

  internal IDisposable BeginRender(scnGame game, bool startingPlayback = false)
  {
    RemovedEffects effects = startingPlayback && _suspended == 0 ? _applied : ForRendering;
    if (_disposed || game == null || game.levelData == null || effects == RemovedEffects.None || _renderDepth > 0)
      return null;
    LevelData original = game.levelData;
    LevelData rendered = EffectLevelFilter.Create(original, effects);
    _renderDepth++;
    game.levelData = rendered;
    return new RenderScope(this, game, original, rendered);
  }

  internal IDisposable SuspendRendering()
  {
    _suspended++;
    return new Suspension(this);
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

  private sealed class Suspension : IDisposable
  {
    private EffectsController _owner;

    public Suspension(EffectsController owner) => _owner = owner;

    public void Dispose()
    {
      if (_owner == null)
        return;
      _owner._suspended--;
      _owner = null;
    }
  }
}
