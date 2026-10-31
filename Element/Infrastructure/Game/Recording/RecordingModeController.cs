using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Element.Infrastructure.Game.Recording;

internal sealed class RecordingModeController
{
  private readonly Dictionary<int, GameObjectState> _gameObjectStates = new();
  private readonly Dictionary<int, BehaviourState> _behaviourStates = new();
  private bool _active;
  private bool _hasNoHudSnapshot;
  private bool _previousNoHud;

  public bool IsActive => _active;

  public void Apply(bool active)
  {
    if (_active == active)
    {
      if (active)
        ReapplyCurrentScene();
      return;
    }

    if (active)
    {
      _previousNoHud = RDC.noHud;
      _hasNoHudSnapshot = true;
      _active = true;
      ReapplyCurrentScene();
      return;
    }

    _active = false;
    RecordingUiPatchState.SuppressNextFlash = false;
    RestoreSceneObjects();
    RestoreNoHud();
  }

  public void ReapplyCurrentScene()
  {
    if (!_active)
      return;

    ForgetDestroyedSceneObjects();
    RDC.noHud = true;

    scrUIController ui = scrUIController.instance;
    if (ui != null)
    {
      Hide(ui.txtLevelName?.gameObject);
      Hide(ui.noFailImage);
      Hide(ui.difficultyImage);
      Hide(ui.difficultyContainer?.gameObject);
      Hide(ui.difficultyFadeContainer?.gameObject);
    }

    scnEditor editor = scnEditor.instance;
    if (editor != null)
    {
      Hide(editor.autoImage);
      Hide(editor.buttonAuto);
      Hide(editor.controlsTip);
      Hide(editor.editorDifficultySelector?.gameObject);
      Hide(editor.buttonNoFail?.gameObject);
    }

    scrController controller = scrController.instance;
    if (controller != null)
    {
      Hide(controller.txtCongrats?.gameObject);
      Hide(controller.txtAllStrictClear?.gameObject);
      Hide(controller.detailedResults?.gameObject);
      Hide(controller.errorMeter?.gameObject);
      Hide(controller.errorMeter?.wrapperRectTransform?.gameObject);
    }

    // Scene objects only. Resources.FindObjectsOfTypeAll also returns prefab
    // assets; deactivating the miss/fail indicator prefabs made every later
    // instance start inactive, so the game's blink coroutine failed on each death.
    scrEnableIfBeta betaIndicator = Object
      .FindObjectsByType<scrEnableIfBeta>(FindObjectsInactive.Include, FindObjectsSortMode.None)
      .FirstOrDefault();
    Hide(betaIndicator?.gameObject);

    foreach (
      scrMissIndicator indicator in Object.FindObjectsByType<scrMissIndicator>(
        FindObjectsInactive.Include,
        FindObjectsSortMode.None
      )
    )
      Hide(indicator?.gameObject);
  }

  public void HideMissIndicator(scrMissIndicator indicator)
  {
    if (_active)
      Hide(indicator?.gameObject);
  }

  public void HideErrorMeter()
  {
    if (!_active)
      return;

    scrHitErrorMeter errorMeter = scrController.instance?.errorMeter;
    Hide(errorMeter?.gameObject);
    Hide(errorMeter?.wrapperRectTransform?.gameObject);
  }

  public void ForgetDestroyedSceneObjects()
  {
    foreach (int id in _gameObjectStates.Where(pair => pair.Value.Target == null).Select(pair => pair.Key).ToArray())
      _gameObjectStates.Remove(id);

    foreach (int id in _behaviourStates.Where(pair => pair.Value.Target == null).Select(pair => pair.Key).ToArray())
      _behaviourStates.Remove(id);
  }

  public void Dispose()
  {
    _active = false;
    RecordingUiPatchState.SuppressNextFlash = false;
    RestoreSceneObjects();
    RestoreNoHud();
  }

  private void Hide(GameObject target)
  {
    if (target == null)
      return;

    int id = target.GetInstanceID();
    if (!_gameObjectStates.ContainsKey(id))
      _gameObjectStates.Add(id, new GameObjectState(target, target.activeSelf));

    if (target.activeSelf)
      target.SetActive(false);
  }

  private void Hide(Behaviour target)
  {
    if (target == null)
      return;

    int id = target.GetInstanceID();
    if (!_behaviourStates.ContainsKey(id))
      _behaviourStates.Add(id, new BehaviourState(target, target.enabled));

    if (target.enabled)
      target.enabled = false;
  }

  private void RestoreSceneObjects()
  {
    foreach (GameObjectState state in _gameObjectStates.Values)
    {
      if (state.Target != null)
        state.Target.SetActive(state.WasActive);
    }

    foreach (BehaviourState state in _behaviourStates.Values)
    {
      if (state.Target != null)
        state.Target.enabled = state.WasEnabled;
    }

    _gameObjectStates.Clear();
    _behaviourStates.Clear();
  }

  private void RestoreNoHud()
  {
    if (!_hasNoHudSnapshot)
      return;

    RDC.noHud = _previousNoHud;
    _hasNoHudSnapshot = false;
  }

  private sealed class GameObjectState
  {
    public GameObject Target { get; }

    public bool WasActive { get; }

    public GameObjectState(GameObject target, bool wasActive)
    {
      Target = target;
      WasActive = wasActive;
    }
  }

  private sealed class BehaviourState
  {
    public Behaviour Target { get; }

    public bool WasEnabled { get; }

    public BehaviourState(Behaviour target, bool wasEnabled)
    {
      Target = target;
      WasEnabled = wasEnabled;
    }
  }
}
