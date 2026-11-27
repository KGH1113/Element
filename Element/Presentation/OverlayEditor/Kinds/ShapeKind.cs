using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Shape;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.ShapeEditor;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>Shapes: a square, triangle or circle picked beside the add menu, edited in the shape editor.</summary>
internal sealed class ShapeKind : ContentElementKind<ShapeOverlayElement>
{
  private const float MenuWidth = 220;
  private readonly ListMenu _menu;

  public ShapeKind(ICanvasContext context, ICanvasActions canvas)
    : base(context, canvas)
  {
    _menu = new ListMenu("Shape kind menu", MenuWidth, UiPalette.Menu);
    context.AddMenu.AddSubmenu(_menu);
  }

  public override string Label(IOverlayLayoutTarget target) => ((ShapeOverlayElement)target).Label;

  public override string KindName(IOverlayLayoutTarget target) => L("도형", "Shape");

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
    Context.AddMenu.OpenSubmenu(
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
    Context.AddMenu.Close();
    ShapeOverlayElement shape = (ShapeOverlayElement)Context.Session.Host.Add(ShapeElementKind.NewSettings(kind));
    shape.SetPreview(true);
    Actions.PlaceNew(shape, false);
    // History records the shape when its editor is done; cancelling removes it.
    Open(shape, true);
  }

  protected override IContentEditor MakeEditor() =>
    new ShapeEditorScreen(
      Context.Session.Root.transform,
      Context.Session.Font,
      Context.Session.ReduceMotion,
      Context.Session.Host.Values
    );

  protected override void Closed(ShapeOverlayElement shape, bool isNew, bool committed) =>
    KeepOrDrop(shape, shape.Settings, isNew, committed);
}
