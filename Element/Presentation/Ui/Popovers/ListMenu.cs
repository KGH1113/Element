using System;
using Element.Presentation.Ui.Declarative;
using ElementUI;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Popovers;

/// <summary>
/// A menu that opens over a screen with an ElementUI screen of rows inside:
/// the layout editor's add menu and the lists beside it. Made when it opens,
/// gone when it hides; as tall as its rows.
/// </summary>
internal sealed class ListMenu : IPopover
{
  private readonly string _name;
  private readonly Color _color;
  private ScreenPanel _panel;

  public ListMenu(string name, float width, Color color)
  {
    _name = name;
    Width = width;
    _color = color;
  }

  public RectTransform Root => _panel?.Root;

  public bool Visible => _panel != null;

  public RectTransform Anchor => null;

  public float Width { get; }

  /// <summary>The open menu's height before it is scaled.</summary>
  public float Height => _panel != null ? _panel.Size.y : 0;

  /// <summary>Runs as it hides, before it goes, even when it was not open.</summary>
  public Action Hiding { get; set; }

  /// <summary>Runs after it went.</summary>
  public Action Hidden { get; set; }

  /// <summary>
  /// Opens on <paramref name="parent"/> showing <paramref name="rows"/>, whose
  /// view model goes with the menu. Returns the first row for the keyboard, or null.
  /// </summary>
  public Selectable Open(Transform parent, IBackend backend, View rows, IDisposable model)
  {
    Hide();
    _panel = new ScreenPanel(_name, parent, _color, keyboard: true);
    _panel.Mount(backend, rows, model, Width);
    return _panel.FirstControl();
  }

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(Root, screen);

  public void Hide()
  {
    Hiding?.Invoke();
    if (_panel == null)
      return;
    _panel.Dispose();
    _panel = null;
    Hidden?.Invoke();
  }
}
