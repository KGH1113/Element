using Element.Domain.Overlay.Text;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Graphics;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The bar's animation switch: values in the selection (or the whole text)
/// grow briefly each time what they show changes; text between them never
/// moves. Turning it on plays it once on the preview.
/// </summary>
internal sealed class TextAnimate
{
  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextColor _color;
  private readonly TextPreview _preview;
  private OverlayBarButtonModel _animateButton;

  public TextAnimate(ITextEditorHost host, TextDocumentEditing text, TextColor color, TextPreview preview)
  {
    _host = host;
    _text = text;
    _color = color;
    _preview = preview;
  }

  /// <summary>The bar's Animate switch; made by <see cref="BuildButton"/>.</summary>
  public OverlayBarButtonModel Button => _animateButton;

  public OverlayBarButtonModel BuildButton() =>
    _animateButton = new OverlayBarButtonModel(
      "Text animation",
      L("애니메이션", "Animate"),
      Toggle,
      icon: UiIconKind.Animate,
      toggle: true
    );

  /// <summary>The switch on, off or halfway for the range; muted with no value to move.</summary>
  public void Show()
  {
    _text.Range(out int start, out int end);
    OverlayAnimateState animate = _text.Document.AnimateState(start, end);
    _animateButton.Set(null, false, false);
    _animateButton.SetSwitch(
      animate == OverlayAnimateState.Mixed ? null : animate == OverlayAnimateState.On,
      animate != OverlayAnimateState.NoValues
    );
  }

  private void Toggle()
  {
    _host.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
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
