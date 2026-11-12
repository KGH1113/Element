using Element.Application.KeyViewer;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Presentation.Layout.Canvas;
using Element.Presentation.Layout.ContentEditors;
using Element.Presentation.Ui.Graphics;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.Kinds;

/// <summary>
/// ElementNote's key viewers in the game: placed and scaled here, edited in
/// ElementNote. Selected alone, the element toolbar switches hand and foot.
/// </summary>
internal sealed class KeyViewerKind : IElementKind
{
  private readonly ILayoutEditing _editing;

  public KeyViewerKind(ILayoutEditing editing) => _editing = editing;

  private IKeyViewerElements Viewers => _editing.Session.KeyViewers;

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
          "ElementNote에서 '얼불춤 안에 표시'를 켜 주세요",
          "Turn on 'Show inside ADOFAI' in ElementNote"
        ),
        _ => L("이 운영체제에서는 게임 안 키뷰어를 쓸 수 없어요", "In-game key viewers aren't available here"),
      },
      viewers == KeyViewerAvailability.Ready,
      Add
    );
  }

  private void Add()
  {
    _editing.AddMenu.Close();
    IOverlayLayoutTarget viewer = Viewers?.Add(OverlayElementKinds.HandKeyViewer);
    if (viewer == null)
    {
      _editing.Status.ShowError(
        L(
          "키뷰어를 추가하지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't add the key viewer. Check that ElementNote is open."
        )
      );
      return;
    }
    viewer.SetEnabled(true);
    viewer.SetPreview(true);
    _editing.PlaceNew(viewer, true);
  }

  public bool Edit(IOverlayLayoutTarget target)
  {
    OpenInApp(target);
    return true;
  }

  // Hand or foot, and Edit, which opens the viewer in ElementNote.
  public LayoutBarScreen Bar(IOverlayLayoutTarget target)
  {
    KeyViewerBarModel model = new(this, target);
    return new LayoutBarScreen(model, new KeyViewerBarView(model));
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
        _editing.Status.ShowError(
          L(
            "ElementNote가 꺼져 있어요. ElementNote를 켠 뒤 다시 눌러 주세요.",
            "ElementNote is closed. Open it, then try again."
          )
        );
        return;
      case KeyViewerEditResult.AppOutdated:
        _editing.Status.ShowError(
          L(
            "ElementNote를 업데이트해야 해요. ElementNote를 끄고 게임을 다시 시작해 주세요.",
            "ElementNote needs an update. Quit it and restart the game."
          )
        );
        return;
      default:
        _editing.Status.ShowError(
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
      _editing.Status.ShowError(
        L(
          "키뷰어를 바꾸지 못했어요. ElementNote가 켜져 있는지 확인해 주세요.",
          "Couldn't switch the key viewer. Check that ElementNote is open."
        )
      );
      return false;
    }
    LayoutSession session = _editing.Session;
    (old as IExternalLayoutOutline)?.SetSelected(false);
    created.SetPreview(true);
    if (session.IndexOf(created) < 0)
      session.AddFrame(created);
    _editing.Selection.Selected = session.IndexOf(created);
    // The bar follows the new viewer instead of being built again for it.
    replaced(created);
    session.Commit();
    _editing.RefreshLayouts();
    _editing.UpdateFrames();
    return true;
  }

  public void Dispose() { }
}
