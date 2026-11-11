using Element.Configuration;
using Element.Presentation.Layout.ContentEditors;
using Element.Presentation.Layout.ContentEditors.Shape;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Settings;
using Element.Presentation.Ui.Popovers;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout.Kinds;

/// <summary>Shapes: a square, triangle or circle picked beside the add menu, edited in the shape editor.</summary>
internal sealed class ShapeKind : ContentElementKind<ShapeOverlayElement>
{
  private const float MenuWidth = 220,
    MenuRow = 48;
  private readonly ListMenu _menu;

  public ShapeKind(ILayoutEditing editing)
    : base(editing)
  {
    _menu = new ListMenu(editing.Session.NewUi, "Shape kind menu", MenuWidth, MenuRow, LayoutColors.Menu);
    editing.AddMenu.AddSubmenu(_menu, () => _menu.HeightFor(3));
  }

  public override string Label(IOverlayLayoutTarget target) => ((ShapeOverlayElement)target).Label;

  public override string EditCaption => L("도형 편집", "Edit shape");

  public override AddMenuRow Row() =>
    new(
      LayoutAction.Shape,
      L("도형", "Shape"),
      L("사각형·삼각형·원으로 글자 뒤 배경을 깔아요", "A square, triangle or circle behind text"),
      true,
      ToggleMenu
    );

  // Square, triangle or circle, beside the add menu's Shape row.
  private void ToggleMenu()
  {
    if (_menu.Visible)
    {
      _menu.Hide();
      return;
    }
    _menu.Open(Editing.Session.Root.transform, 3);
    (OverlayShapeKind Kind, LayoutAction Icon, string Name)[] kinds =
    {
      (OverlayShapeKind.Square, LayoutAction.Square, L("사각형", "Square")),
      (OverlayShapeKind.Triangle, LayoutAction.Triangle, L("삼각형", "Triangle")),
      (OverlayShapeKind.Circle, LayoutAction.Circle, L("원", "Circle")),
    };
    Button first = null;
    for (int i = 0; i < kinds.Length; i++)
    {
      OverlayShapeKind kind = kinds[i].Kind;
      Button button = _menu.AddRow(
        i,
        "Add " + kind,
        kinds[i].Icon,
        11,
        22,
        SettingsPalette.SoftAccent,
        true,
        () => Add(kind)
      );
      _menu.Ui.Text(kinds[i].Name, button.transform, 50, 0, MenuWidth - 76, MenuRow - 4, 15);
      first ??= button;
    }
    Editing.AddMenu.Opened(_menu);
    EventSystem.current?.SetSelectedGameObject(first.gameObject);
  }

  internal void Add(OverlayShapeKind kind)
  {
    Editing.AddMenu.Close();
    ShapeOverlayElement shape = (ShapeOverlayElement)Editing.Session.Host.Add(ShapeElementKind.NewSettings(kind));
    shape.SetPreview(true);
    Editing.PlaceNew(shape, false);
    // History records the shape when its editor is done; cancelling removes it.
    Open(shape, true);
  }

  protected override IContentEditor MakeEditor() =>
    new OverlayShapeEditor(
      Editing.Session.Root.transform,
      Editing.Session.Font,
      Editing.Session.ReduceMotion,
      Editing.Session.Host.Values
    );

  protected override void Closed(ShapeOverlayElement shape, bool isNew, bool committed) =>
    KeepOrDrop(shape, shape.Settings, isNew, committed);
}
