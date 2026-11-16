using System.Collections.Generic;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// Add Element's menu above the toolbar: a row for each kind that offers one,
/// and the lists some rows open beside it (shapes, other mods' elements).
/// </summary>
internal sealed class LayoutAddMenu
{
  private const float Width = 360;
  private readonly LayoutSession _session;
  private readonly LayoutToolbar _toolbar;
  private readonly PopoverStack _popovers;
  private readonly ListMenu _menu;
  private readonly List<ListMenu> _submenus = new();
  private readonly List<AddMenuRow> _rows = new();
  private ElementKinds _kinds;

  public LayoutAddMenu(LayoutSession session, LayoutToolbar toolbar, PopoverStack popovers)
  {
    _session = session;
    _toolbar = toolbar;
    _popovers = popovers;
    _menu = new ListMenu("Add element menu", Width, UiPalette.Menu) { Hiding = CloseSubmenus, Hidden = FocusAddButton };
    popovers.Add(_menu);
  }

  /// <summary>The kinds whose rows the menu shows.</summary>
  public ElementKinds Kinds
  {
    set => _kinds = value;
  }

  public bool IsOpen => _menu.Visible;

  /// <summary>The open menu; null while closed.</summary>
  public RectTransform Root => _menu.Root;

  /// <summary>A list a row opens beside the menu, as tall as its rows.</summary>
  public void AddSubmenu(ListMenu submenu)
  {
    _submenus.Add(submenu);
    _popovers.Add(submenu);
  }

  /// <summary>Opens the menu; returns its first row for the keyboard.</summary>
  public Selectable Open()
  {
    _rows.Clear();
    foreach (IElementKind kind in _kinds.All)
      if (kind.HasRow)
        _rows.Add(kind.Row());
    AddMenuListModel model = new(_rows.ToArray(), large: true);
    Selectable first = _menu.Open(_session.Root.transform, _session.NewBackend(), new AddMenuListView(model), model);
    _popovers.Opened(_menu);
    return first;
  }

  /// <summary>
  /// Opens <paramref name="submenu"/> beside the menu with <paramref name="rows"/>,
  /// in place of another list, with its first row focused. Esc closes it first.
  /// </summary>
  public void OpenSubmenu(ListMenu submenu, IReadOnlyList<AddMenuRow> rows)
  {
    CloseSubmenus();
    AddMenuListModel model = new(rows, large: false);
    Selectable first = submenu.Open(_session.Root.transform, _session.NewBackend(), new AddMenuListView(model), model);
    _popovers.Opened(submenu);
    PlaceSubmenus();
    if (first != null)
      EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  public void Close() => _menu.Hide();

  public void CloseSubmenus()
  {
    foreach (ListMenu submenu in _submenus)
      submenu.Hide();
  }

  /// <summary>Above the toolbar from Add Element, its lists beside it.</summary>
  public void Place()
  {
    if (!_menu.Visible)
      return;
    float height = _menu.Height;
    PopoverPlacement.PlaceScaled(_menu.Root, _toolbar.PopoverAboveAdd(Width, height), Width, height);
    PlaceSubmenus();
  }

  private void PlaceSubmenus()
  {
    foreach (ListMenu submenu in _submenus)
      PlaceBeside(submenu);
  }

  // Beside the menu, bottoms aligned so it sits next to its row and clear of
  // the toolbar; on the left when the view ends on the right.
  private void PlaceBeside(ListMenu submenu)
  {
    if (!submenu.Visible || !_menu.Visible)
      return;
    Rect menu = PopoverPlacement.ScreenRect(_menu.Root);
    float scale = _menu.Root.localScale.x;
    float w = submenu.Width * scale,
      h = submenu.Height * scale;
    PopoverPlacement.PlaceScaled(
      submenu.Root,
      PopoverPlacement.Beside(menu, menu.yMax - h, w, h, 8, _session.ViewWidth, _session.ViewHeight),
      submenu.Width,
      submenu.Height
    );
  }

  private void FocusAddButton()
  {
    if (_session.IsOpen && _toolbar.AddButton != null)
      EventSystem.current?.SetSelectedGameObject(_toolbar.AddButton.gameObject);
  }
}
