using Element.Domain.Overlay.Text;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The column's Animate row and the tool it opens there: values in the
/// selection (or the whole text) grow briefly each time what they show
/// changes, and the selection's color can follow a value
/// (<see cref="TextValueColor"/>). The row says what is on. Turning growth on
/// plays it once on the preview.
/// </summary>
internal sealed class TextAnimate
{
  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextColor _color;
  private readonly TextPreview _preview;
  private readonly TextValueColor _valueColor;
  private OverlayBarButtonModel _animateButton;
  private OverlayBarButtonModel _growRow;
  private OverlayBarButtonModel _colorRow;
  private TextAnimateModel _model;
  private ScreenPanel _panel;
  private OverlayEditorInspector _inspector;
  private bool _colorShown;

  public TextAnimate(
    ITextEditorHost host,
    TextDocumentEditing text,
    TextColor color,
    TextPreview preview,
    TextValueColor valueColor
  )
  {
    _host = host;
    _text = text;
    _color = color;
    _preview = preview;
    _valueColor = valueColor;
  }

  /// <summary>The column's Animate row; made by <see cref="BuildButton"/>.</summary>
  public OverlayBarButtonModel Button => _animateButton;

  /// <summary>The tool's growth switch.</summary>
  public OverlayBarButtonModel GrowRow => _growRow;

  /// <summary>The tool's switch for a color that follows a value.</summary>
  public OverlayBarButtonModel ColorRow => _colorRow;

  /// <summary>The tool is open in the column.</summary>
  public bool Visible => _panel != null && _panel.Root.gameObject.activeSelf;

  public RectTransform Root => _panel?.Root;

  /// <summary>What the column's back row names while the tool is open.</summary>
  public string Title =>
    _text.HasSelection ? L("선택한 글자 애니메이션", "Selected text animation") : L("애니메이션", "Animate");

  public OverlayBarButtonModel BuildButton() =>
    _animateButton = new OverlayBarButtonModel(
      "Text animation",
      L("애니메이션", "Animate"),
      Open,
      value: true,
      icon: UiIconKind.Animate,
      opens: true
    );

  /// <summary>The tool, hidden until the row opens it, under <paramref name="inspector"/>'s back row.</summary>
  public void Build(OverlayEditorFrame screen, OverlayEditorInspector inspector)
  {
    _inspector = inspector;
    _growRow = new OverlayBarButtonModel(
      "Grow on change",
      L("값이 바뀔 때 커지기", "Grow when it changes"),
      ToggleGrow,
      icon: UiIconKind.Animate,
      toggle: true
    );
    _colorRow = new OverlayBarButtonModel(
      "Color by value",
      L("값에 따라 색 바꾸기", "Color by value"),
      _valueColor.Toggle,
      swatch: true,
      toggle: true
    );
    OverlayValueColorModel valueColor = _valueColor.BuildModel(UiPalette.EditorDim);
    _model = new TextAnimateModel(_growRow, _colorRow, valueColor);
    _panel = screen.Panel("Animate tool", Color.clear);
    _panel.Mount(screen.NewBackend(), new TextAnimateView(_model, inspector.Width), _model, inspector.Width);
    _panel.Root.gameObject.SetActive(false);
    _colorShown = false;
    _valueColor.Build(screen, SourceRowRect);
  }

  /// <summary>Whether a screen point (origin bottom left) is over the open tool.</summary>
  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel.Root, screen);

  public void Hide()
  {
    if (!Visible)
      return;
    _panel.Root.gameObject.SetActive(false);
    _valueColor.Closed();
  }

  // The row opens the tool in the column; the back row closes it.
  private void Open()
  {
    _host.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    _panel.Root.gameObject.SetActive(true);
    _panel.Root.SetAsLastSibling();
    _host.Render();
  }

  /// <summary>
  /// The row says what is on for the range; the open tool shows the
  /// switches, the color's controls while it follows a value, and sits
  /// under the column's back row.
  /// </summary>
  public void Show()
  {
    _text.Range(out int start, out int end);
    OverlayAnimateState grow = _text.Document.AnimateState(start, end);
    bool? color = _valueColor.Follows();
    bool growing = grow == OverlayAnimateState.On || grow == OverlayAnimateState.Mixed;
    bool coloring = color != false;
    _animateButton.Set(
      growing && coloring ? L("커지기 · 색", "Grow · Color")
        : growing ? L("커지기", "Grow")
        : coloring ? L("색", "Color")
        : L("꺼짐", "Off"),
      false,
      Visible
    );
    if (!Visible)
      return;
    _growRow.SetSwitch(
      grow == OverlayAnimateState.Mixed ? null : grow == OverlayAnimateState.On,
      grow != OverlayAnimateState.NoValues
    );
    _colorRow.SetSwitch(color, true);
    _valueColor.ShowSwatch(_colorRow);
    _model.ColorShown.Value = coloring;
    if (coloring)
      _valueColor.Show();
    if (_colorShown != coloring)
    {
      _colorShown = coloring;
      _panel.Refit(_inspector.Width);
    }
    _panel.Sync();
    _panel.Root.anchoredPosition = new Vector2(Mathf.Round(_inspector.Left), -Mathf.Round(_inspector.ToolTop));
  }

  /// <summary>The tool's height as laid out now, in frame units.</summary>
  public float Height => _panel?.Size.y ?? 0;

  // Where the color's value row is, in frame units, for the menu under it.
  private Rect SourceRowRect()
  {
    Selectable row = _panel.Control<Selectable>("Value color source");
    return row != null && row.gameObject.activeInHierarchy
      ? _host.Screen.FrameRect((RectTransform)row.transform)
      : new Rect(_inspector.Left, _inspector.ToolTop, _inspector.Width, 0);
  }

  private void ToggleGrow()
  {
    _host.CloseOpen();
    _text.Range(out int start, out int end);
    OverlayAnimateState state = _text.Document.AnimateState(start, end);
    if (state == OverlayAnimateState.NoValues)
    {
      _host.Screen.Notice(
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
    _host.Render();
    if (!animate)
      return;
    _host.Screen.Notice(
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
