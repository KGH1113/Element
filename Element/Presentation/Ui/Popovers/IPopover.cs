using UnityEngine;

namespace Element.Presentation.Ui.Popovers;

/// <summary>A panel that opens over a screen and closes again: a picker, a list, a menu.</summary>
internal interface IPopover
{
  RectTransform Root { get; }

  bool Visible { get; }

  /// <summary>The control it opened from; a press there is not outside it. Null when none.</summary>
  RectTransform Anchor { get; }

  /// <summary>Whether a screen point (origin bottom left) is over it.</summary>
  bool Contains(Vector2 screen);

  void Hide();
}
