using Element.Application.Layout;
using Element.Presentation.Overlay.Canvas;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Canvas;
using Element.Presentation.OverlayEditor.Canvas.AddMenu;
using Element.Presentation.OverlayEditor.TextEditor;
using Element.Presentation.Ui.Graphics;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Kinds;

/// <summary>Texts: added empty and typed in the text editor, sized there.</summary>
internal sealed class TextKind : ContentElementKind<TextOverlayElement>
{
  public TextKind(ICanvasContext context, ICanvasActions canvas)
    : base(context, canvas) { }

  // What it says, so the toolbar and the layer cards tell texts apart.
  public override string Label(IOverlayLayoutTarget target) =>
    target is TextOverlayElement text ? text.Words : L("텍스트", "Text");

  public override string EditCaption => L("텍스트 편집", "Edit text");

  public override AddMenuRow Row()
  {
    bool available = Context.Session.Host.CanShowText;
    return new AddMenuRow(
      UiIconKind.Text,
      L("텍스트", "Text"),
      available
        ? L("글자에 FPS·정확도 같은 값을 넣어요", "Words with values like FPS or accuracy")
        : L("오버레이 글꼴을 불러오지 못해 쓸 수 없어요", "Unavailable: the overlay font didn't load"),
      available,
      Add
    );
  }

  internal void Add()
  {
    Context.AddMenu.Close();
    TextOverlayElement text = (TextOverlayElement)Context.Session.Host.Add(TextElementKind.NewSettings(null));
    if (text == null)
      return;
    text.SetPreview(true);
    Context.Session.AddFrame(text);
    Context.Selection.Selected = Context.Session.Frames.Count - 1;
    // History records the text once it has content; cancelling removes it.
    Open(text, true);
  }

  protected override IContentEditor MakeEditor() =>
    new TextEditorScreen(
      Context.Session.Root.transform,
      Context.Session.Font,
      Context.Session.ReduceMotion,
      Context.Session.Host.Values,
      Context.Session.CustomValues
    );

  // Done with text keeps it (one undo step); a new text that is cancelled, or
  // any text left empty, is removed.
  protected override void Closed(TextOverlayElement text, bool isNew, bool committed)
  {
    if (committed && !text.IsEmpty)
    {
      if (isNew)
        Actions.PlaceNew(text, true);
      else
        Context.Session.Commit();
    }
    else if (isNew || committed)
    {
      text.SetEnabled(false);
      Context.Selection.Selected = -1;
      if (!isNew)
        Context.Session.Commit();
    }
    Actions.RefreshLayouts();
    Actions.UpdateFrames();
  }
}
