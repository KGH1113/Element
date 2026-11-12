using System;
using System.Collections.Generic;
using Element.Presentation.Ui.Input;
using ElementUI;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Declarative;

/// <summary>
/// A rounded panel on one of Element's own canvases with an ElementUI screen
/// inside: the layout editor's toolbars, menus and dialog. The panel is the
/// frame (where it sits, its size, what is in front); the screen is what it
/// shows. The panel owns the screen's view model and lets it go with the screen.
/// </summary>
internal sealed class ScreenPanel : IDisposable
{
  private readonly List<Selectable> _controls = new();
  private readonly bool _keyboard;
  private IDisposable _model;
  private int _mounted = -1;

  /// <param name="color">Its fill; a clear one leaves it unfilled, its controls floating on what is behind.</param>
  /// <param name="keyboard">
  /// Whether its controls take the keyboard (a menu, a dialog). A toolbar's
  /// don't: a click leaves no focus behind, so Enter, Space and the arrows
  /// keep editing the canvas instead of pressing the last button again.
  /// </param>
  /// <param name="radius">Its corners: 8, or half its height for a capsule.</param>
  public ScreenPanel(string name, Transform parent, Color color, bool keyboard, float radius = 8)
  {
    GameObject panel = new(name, typeof(RectTransform));
    panel.layer = parent.gameObject.layer;
    Root = (RectTransform)panel.transform;
    Root.SetParent(parent, false);
    Root.anchorMin = Root.anchorMax = Root.pivot = new Vector2(0, 1);
    if (color.a > 0)
    {
      RoundedRect fill = panel.AddComponent<RoundedRect>();
      fill.Radius = radius;
      fill.color = color;
      fill.raycastTarget = true;
    }
    _keyboard = keyboard;
  }

  public RectTransform Root { get; }

  /// <summary>The mounted screen; null before <see cref="Mount"/>.</summary>
  public UiHost Host { get; private set; }

  /// <summary>Its size before it is scaled: what the screen's content takes.</summary>
  public Vector2 Size { get; private set; }

  /// <summary>Goes up each time the controls are found again: a part shown for the first time added some.</summary>
  public int ControlsVersion { get; private set; }

  /// <summary>
  /// Shows <paramref name="view"/>, replacing the screen there, sized to its
  /// content at <paramref name="width"/> (NaN: as wide as it wants).
  /// <paramref name="model"/> is disposed with the screen; null when its owner keeps it.
  /// </summary>
  public Vector2 Mount(IBackend backend, View view, IDisposable model, float width = float.NaN)
  {
    _model?.Dispose();
    _model = model;
    Root.sizeDelta = new Vector2(float.IsNaN(width) ? 100 : width, 100);
    Host = UiHost.Mount(Root, backend, view);
    ElementUI.Size size = Host.FitContent(width);
    Size = new Vector2(size.Width, size.Height);
    _mounted = -1;
    Sync();
    return Size;
  }

  /// <summary>
  /// Sizes the panel to its screen's content again, at <paramref name="width"/>
  /// (NaN: as wide as it wants), after what it shows changed size.
  /// </summary>
  public Vector2 Refit(float width = float.NaN)
  {
    if (Host == null)
      return Size;
    ElementUI.Size size = Host.FitContent(width);
    Size = new Vector2(size.Width, size.Height);
    return Size;
  }

  /// <summary>
  /// Once a frame, and right after mounting: controls of a part shown for
  /// the first time get the panel's cursor and keyboard rules. Compares a count otherwise.
  /// </summary>
  public void Sync()
  {
    int mounted = Host != null && Host.Root != null ? Host.Root.MountedCount : 0;
    if (mounted == _mounted)
      return;
    _mounted = mounted;
    Root.GetComponentsInChildren(true, _controls);
    foreach (Selectable control in _controls)
    {
      if (!_keyboard)
        control.navigation = new Navigation { mode = Navigation.Mode.None };
      // A drawn part that set its own cursor, such as a color square's crosshair, keeps it.
      if (control.GetComponent<UiCursor>() == null)
        UiCursor.Attach(control.gameObject, control is TMP_InputField ? UiCursorKind.Text : UiCursorKind.Pointer);
    }
    ControlsVersion++;
  }

  /// <summary>The control named <paramref name="name"/> (see <c>Named</c>), or null while it isn't made.</summary>
  public T Control<T>(string name)
    where T : Selectable
  {
    Sync();
    foreach (Selectable control in _controls)
      if (control != null && control is T typed && control.name == name)
        return typed;
    return null;
  }

  /// <summary>The first control that is shown and can be pressed, for the keyboard; null when none.</summary>
  public Selectable FirstControl()
  {
    Sync();
    foreach (Selectable control in _controls)
      if (control != null && control.isActiveAndEnabled && control.IsInteractable())
        return control;
    return null;
  }

  /// <summary>Whether a shown control, which takes the press itself, is under a screen point (origin bottom left).</summary>
  public bool ControlAt(Vector2 screen)
  {
    Sync();
    foreach (Selectable control in _controls)
      if (
        control != null
        && control.gameObject.activeInHierarchy
        && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)control.transform, screen, null)
      )
        return true;
    return false;
  }

  /// <summary>Takes the panel and its screen away at once, and lets the view model go.</summary>
  public void Dispose()
  {
    Root.gameObject.SetActive(false);
    RectPeer.DestroyObject(Root.gameObject);
    Host = null;
    _controls.Clear();
    _model?.Dispose();
    _model = null;
  }
}
