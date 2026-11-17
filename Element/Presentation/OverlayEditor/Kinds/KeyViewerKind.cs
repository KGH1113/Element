using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.Canvas.ElementBar;
using Element.Presentation.Ui.Graphics;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>
/// ElementNote's key viewers in the game: placed and scaled here, edited in
/// ElementNote. Selected alone, the element toolbar switches hand and foot.
/// </summary>
internal sealed class KeyViewerKind : IElementKind
{
  private readonly ICanvasContext _context;
  private readonly ICanvasActions _canvas;

  public KeyViewerKind(ICanvasContext context, ICanvasActions canvas)
  {
    _context = context;
    _canvas = canvas;
  }

  private IKeyViewerElements Viewers => _context.Session.KeyViewers;

  public bool Owns(IOverlayLayoutTarget target) => OverlayElementKinds.IsKeyViewer(target.TemplateId);

  public string Label(IOverlayLayoutTarget target) =>
    target.TemplateId == OverlayElementKinds.FootKeyViewer
      ? L("발 키뷰어", "Foot key viewer")
      : L("손 키뷰어", "Hand key viewer");

  public bool SizedInEditor => false;

  public string EditCaption => null;

  public bool HasRow => true;

  public IContentEditor Editor => null;

  public AddMenuRow Row()
  {
    KeyViewerAvailability viewers = Viewers?.Availability ?? KeyViewerAvailability.Unsupported;
    return new AddMenuRow(
      UiIconKind.KeyViewer,
      L("키뷰어", "Key viewer"),
      viewers switch
      {
        KeyViewerAvailability.Ready => L(
          "ElementNote의 키뷰어를 게임 화면에 놓아요",
          "Places ElementNote's key viewer"
        ),
        KeyViewerAvailability.AppClosed => L(
          "ElementNote를 실행하면 추가할 수 있어요",
          "Open ElementNote to add a key viewer"
        ),
        KeyViewerAvailability.HiddenInApp => L(
          "ElementNote 설정에서 '얼불춤 안'을 켜 주세요",
          "Turn on 'Inside ADOFAI' in ElementNote's settings"
        ),
        _ => L("이 운영체제에서는 게임 안 키뷰어를 쓸 수 없어요", "In-game key viewers aren't available here"),
      },
      viewers == KeyViewerAvailability.Ready,
      Add
    );
  }

  private void Add()
  {
    _context.AddMenu.Close();
    IOverlayLayoutTarget viewer = Viewers?.Add(OverlayElementKinds.HandKeyViewer);
    if (viewer == null)
    {
      _context.Status.ShowError(
        L(
          "키뷰어를 추가하지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't add the key viewer. Check that ElementNote is open."
        )
      );
      return;
    }
    viewer.SetEnabled(true);
    viewer.SetPreview(true);
    _canvas.PlaceNew(viewer, true);
  }

  public bool Edit(IOverlayLayoutTarget target)
  {
    OpenInApp(target);
    return true;
  }

  // Hand or foot, and Edit, which opens the viewer in ElementNote.
  public ElementBarContent Bar(IOverlayLayoutTarget target)
  {
    KeyViewerBarViewModel model = new(this, target);
    return new ElementBarContent(model, new KeyViewerBarView(model));
  }

  // ElementNote edits the viewer; the game brings it forward on the right tab.
  internal void OpenInApp(IOverlayLayoutTarget target)
  {
    KeyViewerEditResult result =
      target == null ? KeyViewerEditResult.Failed : Viewers?.OpenInApp(target) ?? KeyViewerEditResult.Failed;
    switch (result)
    {
      case KeyViewerEditResult.Opened:
        return;
      case KeyViewerEditResult.AppClosed:
        _context.Status.ShowError(
          L(
            "ElementNote가 꺼져 있어요. ElementNote를 켠 뒤 다시 눌러 주세요.",
            "ElementNote is closed. Open it, then try again."
          )
        );
        return;
      case KeyViewerEditResult.AppOutdated:
        _context.Status.ShowError(
          L(
            "ElementNote를 업데이트해야 해요. ElementNote를 끄고 게임을 다시 시작해 주세요.",
            "ElementNote needs an update. Quit it and restart the game."
          )
        );
        return;
      default:
        _context.Status.ShowError(
          L(
            "ElementNote를 열지 못했어요. 잠시 후 다시 눌러 주세요.",
            "Couldn't open ElementNote. Try again in a moment."
          )
        );
        return;
    }
  }

  /// <summary>
  /// Replaces <paramref name="old"/> with a viewer of <paramref name="templateId"/>
  /// in its place, selected, as one undo step; <paramref name="replaced"/> hears
  /// of the new one before the editor catches up. False, with the reason on
  /// the status line, when ElementNote couldn't.
  /// </summary>
  internal bool Switch(IOverlayLayoutTarget old, string templateId, System.Action<IOverlayLayoutTarget> replaced)
  {
    if (old == null || old.TemplateId == templateId)
      return true;
    IOverlayLayoutTarget created = Viewers?.Switch(old, templateId);
    if (created == null)
    {
      _context.Status.ShowError(
        L(
          "키뷰어를 바꾸지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't switch the key viewer. Check that ElementNote is open."
        )
      );
      return false;
    }
    CanvasSession session = _context.Session;
    (old as IExternalLayoutOutline)?.SetSelected(false);
    created.SetPreview(true);
    if (session.IndexOf(created) < 0)
      session.AddFrame(created);
    _context.Selection.Selected = session.IndexOf(created);
    // The bar follows the new viewer instead of being built again for it.
    replaced(created);
    session.Commit();
    _canvas.RefreshLayouts();
    _canvas.UpdateFrames();
    return true;
  }

  public void Dispose() { }
}
