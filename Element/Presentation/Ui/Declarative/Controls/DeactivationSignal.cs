using System;
using UnityEngine;

namespace Element.Presentation.Ui.Declarative.Controls;

/// <summary>
/// Calls back when its GameObject stops being active, as when the window
/// holding it closes: what a popover listens to so it isn't left open.
/// It runs in the editor too, where the UI preview closes windows.
/// </summary>
[ExecuteAlways]
internal sealed class DeactivationSignal : MonoBehaviour
{
  public Action Deactivated;

  private void OnDisable() => Deactivated?.Invoke();
}
