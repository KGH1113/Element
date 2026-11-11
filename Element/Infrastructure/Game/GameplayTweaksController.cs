using System;
using Element.Configuration;
using Element.Infrastructure.Game.Editor;
using Element.Infrastructure.Game.Input;
using Element.Infrastructure.Game.Planets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Element.Infrastructure.Game;

/// <summary>
/// The Tweaks settings in the game: editor tweaks, planet appearance and the chatter
/// blocker's settings. The game patches call it; an error stops only the tweak it
/// came from, which gives the game its own behavior back.
/// </summary>
internal sealed class GameplayTweaksController : IDisposable
{
  private readonly Func<GameplayTweaksSettings> _settings;
  private readonly ChatterBlocker _chatter;
  private readonly Action<string, Exception> _logException;
  private readonly EditorTweaksController _editor;
  private readonly PlanetAppearanceController _planets;
  internal PlanetAppearanceController Planets => _planets;
  private bool _disposed,
    _editorFailed,
    _planetsFailed;

  /// <param name="settings">Normalized when they load; the settings window writes only valid values.</param>
  public GameplayTweaksController(
    Func<GameplayTweaksSettings> settings,
    ChatterBlocker chatter,
    Action<string, Exception> logException
  )
  {
    _settings = settings;
    _chatter = chatter;
    _logException = logException;
    GameplayTweaksSettings gameplay = settings();
    _editor = new(gameplay.Editor);
    _planets = new(gameplay.Planets);
    SceneManager.sceneUnloaded += SceneUnloaded;
  }

  public void Apply()
  {
    if (_disposed)
      return;
    ChatterBlockerOptions chatter = _settings().Chatter;
    _chatter.Configure(chatter.Enabled, chatter.WindowMs, chatter.IgnoredKeys);
    if (!_editorFailed)
    {
      _editor.ApplySettings();
      try
      {
        _editor.Tick(ADOBase.editor);
      }
      catch (Exception exception)
      {
        FailEditor(exception);
      }
    }
    ApplyPlanets();
  }

  private bool EditorOn => !_disposed && !_editorFailed;

  private bool PlanetsOn => !_disposed && !_planetsFailed;

  public void AfterEditorUpdate(scnEditor editor)
  {
    if (!EditorOn)
      return;
    try
    {
      _editor.Tick(editor);
    }
    catch (Exception exception)
    {
      FailEditor(exception);
    }
  }

  public void InvalidateEditor()
  {
    if (!_disposed)
      _editor.Invalidate();
  }

  public void OnFloorVisibilityChanged(scrFloor floor)
  {
    if (!_disposed)
      _editor.OnFloorVisibilityChanged(floor);
  }

  /// <summary>Before the editor flips or rotates <paramref name="floor"/>; null when nothing follows it.</summary>
  public EditorTweaksController.Mutation BeginEditorTransform(
    scnEditor editor,
    scrFloor floor,
    string method,
    object[] args
  )
  {
    if (!EditorOn)
      return null;
    try
    {
      bool direction = method == "RotateFloor180" || (bool)args[1];
      return _editor.BeginMutation(editor, floor, method, direction);
    }
    catch (Exception exception)
    {
      FailEditor(exception);
      return null;
    }
  }

  public void AfterEditorTransform(
    scnEditor editor,
    bool remakePath,
    bool ranOriginal,
    EditorTweaksController.Mutation mutation
  )
  {
    if (!EditorOn || !ranOriginal)
      return;
    try
    {
      _editor.FinishMutation(editor, mutation, remakePath);
    }
    catch (Exception exception)
    {
      FailEditor(exception);
    }
  }

  public void BeforePlanetColor(PlanetRenderer renderer, string method, ref Color color)
  {
    if (!PlanetsOn)
      return;
    try
    {
      _planets.BeforeColor(renderer, method, ref color);
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
    }
  }

  public void AfterPlanetBodyColor(PlanetRenderer renderer)
  {
    if (!PlanetsOn)
      return;
    try
    {
      _planets.AfterBodyColor(renderer);
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
    }
  }

  public PlanetAppearanceController.PlanetState BeginPlanetAppearance(PlanetRenderer renderer)
  {
    if (!PlanetsOn)
      return null;
    try
    {
      return _planets.BeginAppearanceChange(renderer);
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
      return null;
    }
  }

  public void FinishPlanetAppearance(PlanetAppearanceController.PlanetState state)
  {
    if (!PlanetsOn)
      return;
    try
    {
      _planets.EndAppearanceChange(state);
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
    }
  }

  public void AfterPlanetFrame(PlanetRenderer renderer)
  {
    if (!PlanetsOn)
      return;
    try
    {
      _planets.AfterFrame(renderer);
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
    }
  }

  /// <summary>The game loaded or recolored its planets.</summary>
  public void RefreshPlanets() => ApplyPlanets();

  private void ApplyPlanets()
  {
    if (!PlanetsOn)
      return;
    try
    {
      _planets.Apply();
    }
    catch (Exception exception)
    {
      FailPlanets(exception);
    }
  }

  private void FailEditor(Exception exception)
  {
    if (_editorFailed)
      return;
    _editorFailed = true;
    _logException("Editor tweaks stopped after an error; original editor controls remain available", exception);
    try
    {
      _editor.Dispose();
    }
    catch (Exception cleanup)
    {
      _logException("Restore editor selection text", cleanup);
    }
  }

  private void FailPlanets(Exception exception)
  {
    if (_planetsFailed)
      return;
    _planetsFailed = true;
    _logException("Planet appearance stopped after an error; restoring the game's colors", exception);
    try
    {
      _planets.Dispose();
    }
    catch (Exception cleanup)
    {
      _logException("Restore planet appearance", cleanup);
    }
  }

  private void SceneUnloaded(Scene scene)
  {
    _editor.Invalidate();
    _planets.ForgetScene();
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    SceneManager.sceneUnloaded -= SceneUnloaded;
    _chatter.Configure(false, 0, null);
    _editor.Dispose();
    _planets.Dispose();
  }
}
