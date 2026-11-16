using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.ContentEditors;
using Element.Presentation.Layout.ContentEditors.Shape;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Shape;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Kinds;

/// <summary>Shapes: a square, triangle or circle picked beside the add menu, edited in the shape editor.</summary>
internal sealed class ShapeKind : ContentElementKind<ShapeOverlayElement>
{
  private const float MenuWidth = 220;
  private readonly ListMenu _menu;

  public ShapeKind(ILayoutEditing editing)
    : base(editing)
  {
    _menu = new ListMenu("Shape kind menu", MenuWidth, UiPalette.Menu);
    editing.AddMenu.AddSubmenu(_menu);
  }

  public override string Label(IOverlayLayoutTarget target) => ((ShapeOverlayElement)target).Label;

  public override string EditCaption => L("도형 편집", "Edit shape");

  public override AddMenuRow Row() =>
    new(
      UiIconKind.Shape,
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
    Editing.AddMenu.OpenSubmenu(
      _menu,
      new[]
      {
        Row(OverlayShapeKind.Square, UiIconKind.Square, L("사각형", "Square")),
        Row(OverlayShapeKind.Triangle, UiIconKind.Triangle, L("삼각형", "Triangle")),
        Row(OverlayShapeKind.Circle, UiIconKind.Circle, L("원", "Circle")),
      }
    );
  }

  private AddMenuRow Row(OverlayShapeKind kind, UiIconKind icon, string name) =>
    new(icon, name, null, true, () => Add(kind), "Add " + kind);

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
