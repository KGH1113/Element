using System.Collections.Generic;
using Element.Application.Layout;
using Element.Application.Mods;
using Element.Presentation.Layout.ContentEditors;
using Element.Presentation.Layout.ContentEditors.Mods;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Mods;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using ElementKit.Hosting;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Kinds;

/// <summary>
/// Elements other mods add through ElementKit: listed beside the add menu's
/// More row with the mod they come from, edited on their own editing screen.
/// </summary>
internal sealed class ModKind : ContentElementKind<ModOverlayElement>
{
  private const float MenuWidth = 240,
    MenuRow = 56;
  private const int MenuRows = 6;
  private readonly ListMenu _menu;

  public ModKind(ILayoutEditing editing)
    : base(editing)
  {
    _menu = new ListMenu(editing.Session.NewUi, "Mod element menu", MenuWidth, MenuRow, UiPalette.Menu);
    editing.AddMenu.AddSubmenu(_menu, () => _menu.HeightFor(Mathf.Min(MenuRows, Mathf.Max(1, Offered))));
  }

  public override string Label(IOverlayLayoutTarget target) => ((ModOverlayElement)target).Label;

  public override string EditCaption => L("요소 편집", "Edit element");

  private int Offered
  {
    get
    {
      ModElements types = Editing.Session.Host.ModElements;
      if (types == null)
        return 0;
      types.Refresh();
      return types.Offered.Count;
    }
  }

  // A row once a mod offers an element.
  public override bool HasRow => Offered > 0;

  public override AddMenuRow Row()
  {
    int offered = Offered;
    return new AddMenuRow(
      LayoutAction.Extension,
      L("기타", "More"),
      L(
        "다른 모드가 추가한 요소 " + offered + "개",
        offered == 1 ? "1 element from other mods" : offered + " elements from other mods"
      ),
      true,
      ToggleMenu
    );
  }

  // Each type with the mod it comes from, beside the More row.
  private void ToggleMenu()
  {
    if (_menu.Visible)
    {
      _menu.Hide();
      return;
    }
    Editing.AddMenu.CloseSubmenus();
    ModElements types = Editing.Session.Host.ModElements;
    if (types == null)
      return;
    types.Refresh();
    IReadOnlyList<ElementClass> offered = types.Offered;
    _menu.Open(Editing.Session.Root.transform, Mathf.Min(MenuRows, Mathf.Max(1, offered.Count)));
    Button first = null;
    for (int i = 0; i < offered.Count && i < MenuRows; i++)
    {
      ElementClass type = offered[i];
      Button button = _menu.AddRow(
        i,
        "Add " + type.FullKey,
        LayoutAction.Extension,
        15,
        22,
        UiPalette.SoftAccent,
        true,
        () => Add(type)
      );
      TextMeshProUGUI name = _menu.Ui.Text(
        L(type.Info.KoreanName, type.Info.EnglishName),
        button.transform,
        50,
        6,
        MenuWidth - 76,
        22,
        15
      );
      name.textWrappingMode = TextWrappingModes.NoWrap;
      TextMeshProUGUI mod = _menu.Ui.Text(
        type.ModId,
        button.transform,
        50,
        28,
        MenuWidth - 76,
        18,
        12,
        UiPalette.Muted
      );
      mod.textWrappingMode = TextWrappingModes.NoWrap;
      first ??= button;
    }
    Editing.AddMenu.Opened(_menu);
    if (first != null)
      EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  // Like a shape: placed in the middle, its editor open right away.
  internal void Add(ElementClass type)
  {
    Editing.AddMenu.Close();
    ModOverlayElement element = (ModOverlayElement)Editing.Session.Host.Add(ModElementKind.NewSettings(type));
    if (element == null)
      return;
    element.SetPreview(true);
    Editing.PlaceNew(element, false);
    // History records the element when its editor is done; cancelling removes it.
    Open(element, true);
  }

  protected override IContentEditor MakeEditor()
  {
    Canvas.LayoutSession session = Editing.Session;
    return new OverlayModEditor(
      session.Root.transform,
      session.Font,
      session.ReduceMotion,
      () => session.Host.Fonts,
      (type, doing, exception) => session.Host.ModElements?.Failed(type, doing, exception)
    );
  }

  // Its mod is missing, off or failing: say which and what to do.
  protected override void Refused(ModOverlayElement element) => Editing.Status.ShowError(element.Unavailable);

  protected override void Closed(ModOverlayElement element, bool isNew, bool committed) =>
    KeepOrDrop(element, element.Settings, isNew, committed);
}
