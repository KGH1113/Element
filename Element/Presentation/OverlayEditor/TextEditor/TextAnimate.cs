using System;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The column's "바뀔 때 커지기" field, a true-or-false one: values in the
/// selection (or the whole text) grow briefly each time what they show
/// changes. A click opens it in the dock, where the switch sets it; turning
/// it on plays the growth once on the preview. Letters alone have nothing to
/// grow, and saying 참 then says so.
/// </summary>
internal sealed class TextAnimate
{
  private readonly IEditorActions _editor;
  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private PropertyButtonViewModel _animateButton;

  public TextAnimate(IEditorActions editor, TextDocumentEditing text, TextPreview preview)
  {
    _editor = editor;
    _text = text;
    _preview = preview;
    Slot = new FormulaSlot(TextFormulas.GrowKey, OverlayValueType.Bool, Read, Write);
  }

  /// <summary>The column's field; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _animateButton;

  /// <summary>How the dock reads and writes it: true or false.</summary>
  public FormulaSlot Slot { get; }

  /// <param name="open">Opens the field in the dock.</param>
  public PropertyButtonViewModel BuildButton(Action open) =>
    _animateButton = new PropertyButtonViewModel(
      "Text animation",
      L("바뀔 때 커지기", "Grow on change"),
      open,
      field: OverlayValueType.Bool
    );

  /// <summary>The field says whether the range's values grow; lit while the dock has it open.</summary>
  public void Show(bool active)
  {
    _text.Range(out int start, out int end);
    OverlayVariable shown =
      _text.Document.AnimateState(start, end) == OverlayAnimateState.Mixed ? new OverlayVariable() : Read();
    _animateButton.ShowField(shown, "", active);
  }

  private OverlayVariable Read()
  {
    _text.Range(out int start, out int end);
    return new OverlayVariable(
      OverlayPiece.BoolPiece(_text.Document.AnimateState(start, end) == OverlayAnimateState.On)
    );
  }

  // Only a plain 참 or 거짓 is kept: growth is a switch of the text's values.
  private void Write(OverlayVariable variable)
  {
    if (variable.Count != 1 || variable[0].Kind != OverlayPieceKind.Bool)
      return;
    bool animate = variable[0].Bool;
    _text.Range(out int start, out int end);
    OverlayAnimateState state = _text.Document.AnimateState(start, end);
    if (state == OverlayAnimateState.NoValues)
    {
      // Nothing in the range can grow: there is no value to follow.
      if (animate)
        _editor.Refuse();
      return;
    }
    if (state == (animate ? OverlayAnimateState.On : OverlayAnimateState.Off))
      return;
    _text.Remember(false);
    _text.Document.SetAnimate(start, end, animate);
    if (!animate)
      return;
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
