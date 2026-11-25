using Element.Domain.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The column's Animate row and the tool it opens there: values in the
/// selection (or the whole text) grow briefly each time what they show
/// changes. The row says whether they do. Turning growth on plays it once on
/// the preview.
/// </summary>
internal sealed class TextAnimate
{
  private readonly IEditorActions _editor;
  private readonly TextDocumentEditing _text;
  private readonly TextColor _color;
  private readonly TextPreview _preview;
  private PropertyButtonViewModel _animateButton;
  private PropertyButtonViewModel _growRow;
  private TextAnimateViewModel _model;
  private ScreenPanel _panel;
  private InspectorPanel _inspector;

  public TextAnimate(IEditorActions editor, TextDocumentEditing text, TextColor color, TextPreview preview)
  {
    _editor = editor;
    _text = text;
    _color = color;
    _preview = preview;
  }

  /// <summary>The column's Animate row; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _animateButton;

  /// <summary>The tool's growth switch.</summary>
  public PropertyButtonViewModel GrowRow => _growRow;

  /// <summary>The tool is open in the column.</summary>
  public bool Visible => _panel != null && _panel.Root.gameObject.activeSelf;

  public RectTransform Root => _panel?.Root;

  /// <summary>What the column's back row names while the tool is open.</summary>
  public string Title =>
    _text.HasSelection ? L("선택한 글자 애니메이션", "Selected text animation") : L("애니메이션", "Animate");

  public PropertyButtonViewModel BuildButton() =>
    _animateButton = new PropertyButtonViewModel(
      "Text animation",
      L("애니메이션", "Animate"),
      Open,
      value: true,
      icon: UiIconKind.Animate,
      opens: true
    );

  /// <summary>The tool, hidden until the row opens it, under <paramref name="inspector"/>'s back row.</summary>
  public void Build(EditorFramePanel screen, InspectorPanel inspector)
  {
    _inspector = inspector;
    _growRow = new PropertyButtonViewModel(
      "Grow on change",
      L("값이 바뀔 때 커지기", "Grow when it changes"),
      ToggleGrow,
      icon: UiIconKind.Animate,
      toggle: true
    );
    _model = new TextAnimateViewModel(_growRow);
    _panel = screen.Panel("Animate tool", Color.clear);
    _panel.Mount(screen.NewBackend(), new TextAnimateView(_model, inspector.Width), _model, inspector.Width);
    _panel.Root.gameObject.SetActive(false);
  }

  /// <summary>Whether a screen point (origin bottom left) is over the open tool.</summary>
  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel.Root, screen);

  public void Hide()
  {
    if (Visible)
      _panel.Root.gameObject.SetActive(false);
  }

  // The row opens the tool in the column; the back row closes it.
  private void Open()
  {
    _editor.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    _panel.Root.gameObject.SetActive(true);
    _panel.Root.SetAsLastSibling();
    _editor.Render();
  }

  /// <summary>
  /// The row says whether the range's values grow; the open tool shows the
  /// switch and sits under the column's back row.
  /// </summary>
  public void Show()
  {
    _text.Range(out int start, out int end);
    OverlayAnimateState grow = _text.Document.AnimateState(start, end);
    bool growing = grow == OverlayAnimateState.On || grow == OverlayAnimateState.Mixed;
    _animateButton.Set(growing ? L("커지기", "Grow") : L("꺼짐", "Off"), false, Visible);
    if (!Visible)
      return;
    _growRow.SetSwitch(
      grow == OverlayAnimateState.Mixed ? null : grow == OverlayAnimateState.On,
      grow != OverlayAnimateState.NoValues
    );
    _panel.Sync();
    _panel.Root.anchoredPosition = new Vector2(Mathf.Round(_inspector.Left), -Mathf.Round(_inspector.ToolTop));
  }

  /// <summary>The tool's height as laid out now, in frame units.</summary>
  public float Height => _panel?.Size.y ?? 0;

  private void ToggleGrow()
  {
    _editor.CloseOpen();
    _text.Range(out int start, out int end);
    OverlayAnimateState state = _text.Document.AnimateState(start, end);
    if (state == OverlayAnimateState.NoValues)
    {
      _editor.Notice(
        _text.HasSelection
          ? L(
            "고른 부분에 값이 없어요. 값까지 함께 고르면 그 값이 바뀔 때마다 움직여요.",
            "The selection has no value. Include a value to make it move each time it changes."
          )
          : L(
            "아직 움직일 값이 없어요. 값을 넣으면 그 값이 바뀔 때마다 움직여요.",
            "There's no value to move yet. Add one and it moves each time it changes."
          )
      );
      return;
    }
    bool animate = state != OverlayAnimateState.On;
    _text.Remember(false);
    _text.Document.SetAnimate(start, end, animate);
    _editor.Render();
    if (!animate)
      return;
    _editor.Notice(
      L(
        "값이 바뀔 때마다 잠깐 커졌다가 돌아와요.",
        "Each time the value changes, it grows for a moment and settles back."
      ),
      false
    );
    // Every value in the preview is a link, in order.
    int link = 0;
    for (int i = 0; i < end; i++)
    {
      if (!_text.Document[i].IsValue)
        continue;
      if (i >= start)
        _preview.Pulse.Play(link);
      link++;
    }
  }
}
