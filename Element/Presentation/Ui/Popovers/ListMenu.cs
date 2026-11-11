using System;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.Ui.Popovers;

/// <summary>
/// A menu of rows, each a button with an icon and its text: the layout
/// editor's add menu and the lists beside it. Made when it opens, gone when it hides.
/// </summary>
internal sealed class ListMenu : IPopover
{
  private readonly Func<UiFactory> _newUi;
  private readonly string _name;
  private readonly Color _color;
  private UiFactory _ui;

  /// <param name="newUi">Makes the factory for one opening; it is let go with the menu.</param>
  /// <param name="row">One row's height, its gap included.</param>
  public ListMenu(Func<UiFactory> newUi, string name, float width, float row, Color color)
  {
    _newUi = newUi;
    _name = name;
    Width = width;
    RowHeight = row;
    _color = color;
  }

  public RectTransform Root { get; private set; }

  public bool Visible => Root != null;

  public bool IsTyping => false;

  public RectTransform Anchor => null;

  public float Width { get; }

  public float RowHeight { get; }

  /// <summary>The factory of the open menu's rows.</summary>
  public UiFactory Ui => _ui;

  /// <summary>Runs as it hides, before it goes, even when it was not open.</summary>
  public Action Hiding { get; set; }

  /// <summary>Runs after it went.</summary>
  public Action Hidden { get; set; }

  /// <summary>The menu's height for <paramref name="rows"/> rows.</summary>
  public float HeightFor(int rows) => 16 + RowHeight * rows;

  public void Open(Transform parent, int rows)
  {
    _ui = _newUi();
    Image panel = _ui.Panel(_name, parent, 0, 0, Width, HeightFor(rows), _color);
    panel.raycastTarget = true;
    Root = panel.rectTransform;
  }

  /// <summary>Row <paramref name="index"/>: a button with its icon; the caller adds its text.</summary>
  public Button AddRow(
    int index,
    string name,
    UiIconKind icon,
    float iconTop,
    float iconSize,
    Color iconColor,
    bool enabled,
    Action pressed
  )
  {
    Button button = _ui.Button("", Root, 8, 8 + index * RowHeight, Width - 16, RowHeight - 4, pressed);
    button.name = name;
    button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
    button.interactable = enabled;
    button.GetComponentInChildren<TextMeshProUGUI>().gameObject.SetActive(false);
    UiIcon glyph = _ui.Rect("Icon", button.transform, 14, iconTop, iconSize, iconSize)
      .gameObject.AddComponent<UiIcon>();
    glyph.Kind = icon;
    glyph.color = iconColor;
    glyph.raycastTarget = false;
    return button;
  }

  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(Root, screen);

  public void Hide()
  {
    Hiding?.Invoke();
    if (Root == null)
      return;
    Root.gameObject.SetActive(false);
    UiFactory.Destroy(Root.gameObject);
    Root = null;
    _ui?.Dispose();
    _ui = null;
    Hidden?.Invoke();
  }
}
