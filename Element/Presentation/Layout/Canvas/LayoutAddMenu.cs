using System;
using System.Collections.Generic;
using Element.Presentation.Layout.Kinds;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Element.Presentation.Layout.Canvas;

/// <summary>
/// Add Element's menu above the toolbar: a row for each kind that offers one,
/// and the lists some rows open beside it.
/// </summary>
internal sealed class LayoutAddMenu
{
  private const float Width = 360,
    Row = 68,
    ToolbarX = 348;
  private readonly LayoutSession _session;
  private readonly LayoutToolbar _toolbar;
  private readonly PopoverStack _popovers;
  private readonly ListMenu _menu;
  private readonly List<ListMenu> _submenus = new();
  private readonly List<Func<float>> _heights = new();
  private ElementKinds _kinds;

  public LayoutAddMenu(LayoutSession session, LayoutToolbar toolbar, PopoverStack popovers)
  {
    _session = session;
    _toolbar = toolbar;
    _popovers = popovers;
    _menu = new ListMenu(session.NewUi, "Add element menu", Width, Row, UiPalette.Menu)
    {
      Hiding = CloseSubmenus,
      Hidden = FocusAddButton,
    };
    popovers.Add(_menu);
  }

  /// <summary>The kinds whose rows the menu shows.</summary>
  public ElementKinds Kinds
  {
    set => _kinds = value;
  }

  public bool IsOpen => _menu.Visible;

  /// <summary>A list a row opens beside the menu; <paramref name="height"/> is read each time it is placed.</summary>
  public void AddSubmenu(ListMenu submenu, Func<float> height)
  {
    _submenus.Add(submenu);
    _heights.Add(height);
    _popovers.Add(submenu);
  }

  /// <summary>Opens the menu; returns its first row for the keyboard.</summary>
  public Button Open()
  {
    _menu.Open(_session.Root.transform, _kinds.Rows);
    Button first = null;
    int row = 0;
    foreach (IElementKind kind in _kinds.All)
    {
      if (!kind.HasRow)
        continue;
      Button button = AddRow(row++, kind.Row());
      first ??= button;
    }
    _popovers.Opened(_menu);
    return first;
  }

  private Button AddRow(int index, AddMenuRow row)
  {
    SettingsUiFactory ui = _menu.Ui;
    Button button = _menu.AddRow(
      index,
      "Add " + row.Icon,
      row.Icon,
      18,
      28,
      row.Enabled ? UiPalette.SoftAccent : UiPalette.Muted,
      row.Enabled,
      row.Pressed
    );
    ui.Text(row.Title, button.transform, 56, 10, Width - 88, 24, 16);
    TextMeshProUGUI detail = ui.Text(
      row.Detail,
      button.transform,
      56,
      34,
      Width - 88,
      20,
      12,
      row.Enabled ? UiPalette.Muted : UiPalette.Error
    );
    detail.textWrappingMode = TextWrappingModes.NoWrap;
    return button;
  }

  public void Close() => _menu.Hide();

  public void CloseSubmenus()
  {
    foreach (ListMenu submenu in _submenus)
      submenu.Hide();
  }

  /// <summary>Call after a list opened beside the menu: Esc closes it first, and it is placed.</summary>
  public void Opened(ListMenu submenu)
  {
    _popovers.Opened(submenu);
    PlaceSubmenus();
  }

  /// <summary>Above the toolbar from Add Element, its lists beside it.</summary>
  public void Place()
  {
    if (!_menu.Visible)
      return;
    float height = _menu.HeightFor(_kinds.Rows);
    PopoverPlacement.PlaceScaled(_menu.Root, _toolbar.PopoverAbove(Width, height, ToolbarX, false), Width, height);
    PlaceSubmenus();
  }

  private void PlaceSubmenus()
  {
    for (int i = 0; i < _submenus.Count; i++)
      PlaceBeside(_submenus[i], _heights[i]());
  }

  // Beside the menu, bottoms aligned so it sits next to its row and clear of
  // the toolbar; on the left when the view ends on the right.
  private void PlaceBeside(ListMenu submenu, float height)
  {
    if (!submenu.Visible || !_menu.Visible)
      return;
    Rect menu = PopoverPlacement.ScreenRect(_menu.Root);
    float scale = _menu.Root.localScale.x;
    float w = submenu.Width * scale,
      h = height * scale;
    PopoverPlacement.PlaceScaled(
      submenu.Root,
      PopoverPlacement.Beside(menu, menu.yMax - h, w, h, 8, _session.ViewWidth, _session.ViewHeight),
      submenu.Width,
      height
    );
  }

  private void FocusAddButton()
  {
    if (_session.IsOpen && _toolbar.AddButton != null)
      EventSystem.current?.SetSelectedGameObject(_toolbar.AddButton.gameObject);
  }
}
