using System;
using System.Reflection;
using HarmonyLib;
using ImplResourcePack.Infrastructure.Game.Editor;
using ImplResourcePack.Infrastructure.Game.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ImplResourcePack.Infrastructure.Game.Tweaks;

internal sealed class GameplayTweaksController : IDisposable
{
  private static GameplayTweaksController _current;
  private readonly Main _main;
  private readonly Harmony _harmony = new("ImplResourcePack.GameplayTweaks");
  private readonly EditorTweaksController _editor;
  private readonly PlanetAppearanceController _planets;
  internal PlanetAppearanceController Planets => _planets;
  private bool _disposed,
    _editorFailed,
    _planetsFailed;

  public GameplayTweaksController(Main main)
  {
    _main = main;
    main.Settings.Gameplay.Normalize();
    _editor = new(main.Settings.Gameplay.Editor);
    _planets = new(main.Settings.Gameplay.Planets);
    _current = this;
    Hook(typeof(scnEditor), "Update", postfix: nameof(AfterEditorUpdate));
    Hook(typeof(scnEditor), "OnSelectedFloorChange", postfix: nameof(InvalidateEditor));
    Hook(typeof(scnEditor), "RemakePath", postfix: nameof(InvalidateEditor));
    Hook(typeof(scnEditor), "DrawFloorNums", postfix: nameof(InvalidateEditor));
    Hook(typeof(scrFloor), "OnBecameVisible", postfix: nameof(FloorVisibilityChanged));
    Hook(typeof(scrFloor), "OnBecameInvisible", postfix: nameof(FloorVisibilityChanged));
    foreach (string method in new[] { "FlipFloor", "RotateFloor", "RotateFloor180" })
      Hook(
        typeof(scnEditor),
        method,
        nameof(BeforeEditorTransform),
        nameof(AfterEditorTransform),
        nameof(FinishEditorTransform)
      );
    foreach (
      string method in new[] { "SetPlanetColor", "SetCoreColor", "SetTailColor", "SetRingColor", "SetFaceColor" }
    )
      Hook(
        typeof(PlanetRenderer),
        method,
        nameof(BeforePlanetColor),
        method == "SetPlanetColor" ? nameof(AfterPlanetBodyColor) : null
      );
    foreach (
      string method in new[]
      {
        "SetColor",
        "LoadPlanetColor",
        "EnableCustomColor",
        "EnableDefaultFireAndIceColor",
        "ToggleSpecialPlanet",
      }
    )
      Hook(typeof(PlanetRenderer), method, nameof(BeforePlanetAppearance), finalizer: nameof(FinishPlanetAppearance));
    Hook(typeof(PlanetRenderer), "LateUpdate", postfix: nameof(AfterPlanetFrame));
    Hook(typeof(PlanetarySystem), "LoadPlanetColors", postfix: nameof(RefreshPlanets));
    Hook(typeof(PlanetarySystem), "ApplyMultiplanetColors", postfix: nameof(RefreshPlanets));
    SceneManager.sceneUnloaded += SceneUnloaded;
  }

  private void Hook(Type type, string method, string prefix = null, string postfix = null, string finalizer = null)
  {
    try
    {
      MethodInfo original = AccessTools.Method(type, method);
      if (original == null)
      {
        _main.LogWarning($"Gameplay tweak hook unavailable: {type.Name}.{method}");
        return;
      }
      _harmony.Patch(original, Method(prefix), Method(postfix), finalizer: Method(finalizer));
    }
    catch (Exception exception)
    {
      // An optional game-version hook must not prevent the mod or other tweaks loading.
      _main.LogException($"Gameplay tweak hook unavailable: {type.Name}.{method}", exception);
    }
  }

  private static HarmonyMethod Method(string name) =>
    name == null ? null : new HarmonyMethod(typeof(GameplayTweaksController), name) { priority = Priority.Last };

  public void Apply()
  {
    if (_disposed)
      return;
    _main.Settings.Gameplay.Normalize();
    ChatterBlocker.Configure(_main.Settings.Gameplay.Chatter.Enabled, _main.Settings.Gameplay.Chatter.WindowMs);
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

  private void ApplyPlanets()
  {
    if (_disposed || _planetsFailed)
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
    _main.LogException("Editor tweaks stopped after an error; original editor controls remain available", exception);
    try
    {
      _editor.Dispose();
    }
    catch (Exception cleanup)
    {
      _main.LogException("Restore editor selection text", cleanup);
    }
  }

  private void FailPlanets(Exception exception)
  {
    if (_planetsFailed)
      return;
    _planetsFailed = true;
    _main.LogException("Planet appearance stopped after an error; restoring the game's colors", exception);
    try
    {
      _planets.Dispose();
    }
    catch (Exception cleanup)
    {
      _main.LogException("Restore planet appearance", cleanup);
    }
  }

  private static void AfterEditorUpdate(scnEditor __instance)
  {
    GameplayTweaksController current = _current;
    if (current == null || current._editorFailed)
      return;
    try
    {
      current._editor.Tick(__instance);
    }
    catch (Exception exception)
    {
      current.FailEditor(exception);
    }
  }

  private static void InvalidateEditor() => _current?._editor.Invalidate();

  private static void FloorVisibilityChanged(scrFloor __instance) =>
    _current?._editor.OnFloorVisibilityChanged(__instance);

  private static void BeforeEditorTransform(
    scnEditor __instance,
    scrFloor floor,
    MethodBase __originalMethod,
    object[] __args,
    out EditorTweaksController.Mutation __state
  )
  {
    __state = null;
    GameplayTweaksController current = _current;
    if (current == null || current._editorFailed)
      return;
    try
    {
      bool direction = __originalMethod.Name == "RotateFloor180" || (bool)__args[1];
      __state = current._editor.BeginMutation(__instance, floor, __originalMethod.Name, direction);
    }
    catch (Exception exception)
    {
      current.FailEditor(exception);
    }
  }

  private static void AfterEditorTransform(
    scnEditor __instance,
    bool remakePath,
    bool __runOriginal,
    EditorTweaksController.Mutation __state
  )
  {
    GameplayTweaksController current = _current;
    if (current == null || current._editorFailed || !__runOriginal)
      return;
    try
    {
      current._editor.FinishMutation(__instance, __state, remakePath);
    }
    catch (Exception exception)
    {
      current.FailEditor(exception);
    }
  }

  private static Exception FinishEditorTransform(Exception __exception, EditorTweaksController.Mutation __state)
  {
    __state?.Dispose();
    return __exception;
  }

  private static void BeforePlanetColor(PlanetRenderer __instance, MethodBase __originalMethod, ref Color color)
  {
    GameplayTweaksController current = _current;
    if (current == null || current._planetsFailed)
      return;
    try
    {
      current._planets.BeforeColor(__instance, __originalMethod.Name, ref color);
    }
    catch (Exception exception)
    {
      current.FailPlanets(exception);
    }
  }

  private static void AfterPlanetBodyColor(PlanetRenderer __instance)
  {
    GameplayTweaksController current = _current;
    if (current == null || current._planetsFailed)
      return;
    try
    {
      current._planets.AfterBodyColor(__instance);
    }
    catch (Exception exception)
    {
      current.FailPlanets(exception);
    }
  }

  private static void BeforePlanetAppearance(
    PlanetRenderer __instance,
    out PlanetAppearanceController.PlanetState __state
  )
  {
    __state = null;
    GameplayTweaksController current = _current;
    if (current == null || current._planetsFailed)
      return;
    try
    {
      __state = current._planets.BeginAppearanceChange(__instance);
    }
    catch (Exception exception)
    {
      current.FailPlanets(exception);
    }
  }

  private static Exception FinishPlanetAppearance(Exception __exception, PlanetAppearanceController.PlanetState __state)
  {
    GameplayTweaksController current = _current;
    if (current != null && !current._planetsFailed)
    {
      try
      {
        current._planets.EndAppearanceChange(__state);
      }
      catch (Exception exception)
      {
        current.FailPlanets(exception);
      }
    }
    return __exception;
  }

  private static void AfterPlanetFrame(PlanetRenderer __instance)
  {
    GameplayTweaksController current = _current;
    if (current == null || current._planetsFailed)
      return;
    try
    {
      current._planets.AfterFrame(__instance);
    }
    catch (Exception exception)
    {
      current.FailPlanets(exception);
    }
  }

  private static void RefreshPlanets() => _current?.ApplyPlanets();

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
    if (_current == this)
      _current = null;
    SceneManager.sceneUnloaded -= SceneUnloaded;
    _harmony.UnpatchAll(_harmony.Id);
    ChatterBlocker.Configure(false, 0);
    _editor.Dispose();
    _planets.Dispose();
  }
}
