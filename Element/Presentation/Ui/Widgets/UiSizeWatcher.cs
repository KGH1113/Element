using System;
using UnityEngine.EventSystems;

namespace Element.Presentation.Ui.Widgets;

/// <summary>Tells its owner when the rect it sits on changes size, as a list's content does when it shows less.</summary>
internal sealed class UiSizeWatcher : UIBehaviour
{
  /// <summary>Called after the rect's size changed.</summary>
  public Action Changed;

  protected override void OnRectTransformDimensionsChange() => Changed?.Invoke();
}
