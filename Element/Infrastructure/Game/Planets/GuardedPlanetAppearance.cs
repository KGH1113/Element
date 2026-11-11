using System;
using Element.Configuration;
using UnityEngine;
using UnityEngine.SceneManagement;
using Element.Configuration.Gameplay;

namespace Element.Infrastructure.Game.Planets;

/// <summary>
/// Planet appearance as the game patches reach it. An error stops it for the rest
/// of the session and gives the planets the game's own colors back.
/// </summary>
internal sealed class GuardedPlanetAppearance : IDisposable
{
  private readonly PlanetAppearanceController _planets;
  private readonly Action<string, Exception> _logException;
  private bool _disposed,
    _failed;

  /// <param name="settings">Normalized when they load; the settings window writes only valid values.</param>
  public GuardedPlanetAppearance(PlanetAppearanceOptions settings, Action<string, Exception> logException)
  {
    _logException = logException;
    _planets = new(settings);
    SceneManager.sceneUnloaded += SceneUnloaded;
  }

  /// <summary>The planets the settings preview draws from.</summary>
  internal PlanetAppearanceController Planets => _planets;

  private bool On => !_disposed && !_failed;

  /// <summary>The settings changed, or the game loaded or recolored its planets.</summary>
  public void Apply()
  {
    if (!On)
      return;
    try
    {
      _planets.Apply();
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void BeforePlanetColor(PlanetRenderer renderer, string method, ref Color color)
  {
    if (!On)
      return;
    try
    {
      _planets.BeforeColor(renderer, method, ref color);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void AfterPlanetBodyColor(PlanetRenderer renderer)
  {
    if (!On)
      return;
    try
    {
      _planets.AfterBodyColor(renderer);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public PlanetOriginalAppearance BeginPlanetAppearance(PlanetRenderer renderer)
  {
    if (!On)
      return null;
    try
    {
      return _planets.BeginAppearanceChange(renderer);
    }
    catch (Exception exception)
    {
      Fail(exception);
      return null;
    }
  }

  public void FinishPlanetAppearance(PlanetOriginalAppearance state)
  {
    if (!On)
      return;
    try
    {
      _planets.EndAppearanceChange(state);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  public void AfterPlanetFrame(PlanetRenderer renderer)
  {
    if (!On)
      return;
    try
    {
      _planets.AfterFrame(renderer);
    }
    catch (Exception exception)
    {
      Fail(exception);
    }
  }

  private void Fail(Exception exception)
  {
    if (_failed)
      return;
    _failed = true;
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

  private void SceneUnloaded(Scene scene) => _planets.ForgetScene();

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    SceneManager.sceneUnloaded -= SceneUnloaded;
    _planets.Dispose();
  }
}
