using System;
using System.Collections.Generic;
using UnityEngine;

namespace Element.Presentation.Ui.Popovers;

/// <summary>
/// The popovers of one screen, in the order they stack. Esc closes the one
/// opened last; a press outside closes them; each frame they are raised over
/// the screen in order. Panels that stay open (a toolbar that follows the
/// selection) are kept too, for raising and hit tests, but never closed.
/// </summary>
internal sealed class PopoverStack
{
  private readonly List<IPopover> _popovers = new();
  private readonly List<bool> _dismissible = new();
  private readonly List<IPopover> _opened = new();
  private readonly Func<IPopover, Vector2, bool> _contains;

  /// <param name="contains">How a screen point (origin bottom left) is tested against a popover; its own test by default.</param>
  public PopoverStack(Func<IPopover, Vector2, bool> contains = null) => _contains = contains ?? OwnTest;

  /// <summary>Adds <paramref name="popover"/> above those added before.</summary>
  public void Add(IPopover popover, bool dismissible = true)
  {
    _popovers.Add(popover);
    _dismissible.Add(dismissible);
  }

  public int Count => _popovers.Count;

  public IPopover this[int index] => _popovers[index];

  /// <summary>Call after showing <paramref name="popover"/>: Esc closes it before those opened earlier.</summary>
  public void Opened(IPopover popover)
  {
    _opened.Remove(popover);
    _opened.Add(popover);
  }

  /// <summary>Whether a popover that closes on Esc is open.</summary>
  public bool AnyOpen
  {
    get
    {
      for (int i = 0; i < _popovers.Count; i++)
        if (_dismissible[i] && _popovers[i].Visible)
          return true;
      return false;
    }
  }

  public bool Typing
  {
    get
    {
      for (int i = 0; i < _popovers.Count; i++)
        if (_popovers[i].Visible && _popovers[i].IsTyping)
          return true;
      return false;
    }
  }

  /// <summary>Whether a screen point (origin bottom left) is over a shown popover or panel.</summary>
  public bool Contains(Vector2 screen)
  {
    for (int i = 0; i < _popovers.Count; i++)
      if (_popovers[i].Visible && _contains(_popovers[i], screen))
        return true;
    return false;
  }

  /// <summary>Closes the open popover that was opened last.</summary>
  public void CloseTop()
  {
    for (int i = _opened.Count - 1; i >= 0; i--)
    {
      IPopover popover = _opened[i];
      _opened.RemoveAt(i);
      if (popover.Visible)
      {
        popover.Hide();
        return;
      }
    }
  }

  /// <summary>Closes every popover that closes on Esc.</summary>
  public void CloseAll()
  {
    for (int i = 0; i < _popovers.Count; i++)
      if (_dismissible[i])
        _popovers[i].Hide();
    _opened.Clear();
  }

  /// <summary>A press at <paramref name="screen"/> closes each open popover it is outside of, and outside its anchor.</summary>
  public void PressOutside(Vector2 screen)
  {
    for (int i = 0; i < _popovers.Count; i++)
    {
      IPopover popover = _popovers[i];
      if (
        _dismissible[i]
        && popover.Visible
        && !_contains(popover, screen)
        && !PopoverPlacement.Contains(popover.Anchor, screen)
      )
        popover.Hide();
    }
  }

  /// <summary>Raises the shown popovers over the screen, in their order.</summary>
  public void Raise()
  {
    for (int i = 0; i < _popovers.Count; i++)
      if (_popovers[i].Visible)
        _popovers[i].Root.SetAsLastSibling();
  }

  private static bool OwnTest(IPopover popover, Vector2 screen) => popover.Contains(screen);
}
