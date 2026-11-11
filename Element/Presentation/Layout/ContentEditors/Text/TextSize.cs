using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Text;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Ui.Widgets;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// The size in the bar: the selection's, or the whole text's with nothing
/// selected, in pixels as the game draws it. The dock types it; a formula
/// sizes it from values, with the plain size kept underneath.
/// </summary>
internal sealed class TextSize
{
  public const float Minimum = 4,
    Maximum = 400;

  private readonly ITextEditorHost _host;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private OverlayBarButton _sizeButton;

  public TextSize(ITextEditorHost host, TextDocumentEditing text, TextFormulas formulas)
  {
    _host = host;
    _text = text;
    _formulas = formulas;
    Slot = new OverlayFormulaSlot(TextFormulas.SizeKey, false, Read, Write);
  }

  /// <summary>How the keypad reads and writes the size.</summary>
  public OverlayFormulaSlot Slot { get; }

  public OverlayBarButton Button => _sizeButton;

  public OverlayBarButton BuildButton(UiFactory ui, Transform bar, bool reduceMotion) =>
    _sizeButton = new OverlayBarButton(
      ui,
      bar,
      "Text size",
      L("크기", "Size"),
      () => _formulas.Activate(TextFormulas.SizeKey),
      reduceMotion,
      "px",
      glyph: L("가", "Aa")
    );

  // The size shows its result; one worked out from a formula reads in the accent.
  public void Show()
  {
    bool active = _formulas.Edits(TextFormulas.SizeKey, out OverlayFormula typed);
    OverlayFormula formula = typed ?? Read();
    bool computed = !formula.IsEmpty && !formula.IsPlainNumber(out _);
    _sizeButton.Set(formula.IsEmpty ? "—" : OverlayFormulaField.Number(_formulas.Result(formula)), computed, active);
  }

  private OverlayFormula Read()
  {
    _text.Range(out int start, out int end);
    OverlayTextItem? first = start < end ? _text.Document[start] : null;
    string formula = first?.SizeFormula ?? _text.TypingSizeFormula(_text.Caret);
    if (!string.IsNullOrEmpty(formula))
      return OverlayFormula.Parse(formula);
    return SameSize(start, end)
      ? OverlayFormula.FromNumber(Mathf.Round(ShownSize(start, end) * 10) / 10)
      : new OverlayFormula();
  }

  private void Write(OverlayFormula formula)
  {
    _text.RememberFormula(TextFormulas.SizeKey);
    _text.Range(out int start, out int end);
    float size;
    string text = "";
    if (formula.IsPlainNumber(out double pixels))
      size = Mathf.Clamp((float)pixels, Minimum, Maximum) / _host.ElementScale;
    else
    {
      // The plain size stays underneath for when the values have no data.
      size = start < end ? _text.Document[start].Size : _text.TypingSize(_text.Caret);
      text = formula.Serialize();
    }
    if (start < end)
      _text.Document.SetSize(start, end, size, text);
    else
    {
      _text.PendingSize = size;
      _text.PendingSizeFormula = text;
    }
  }

  private bool SameSize(int start, int end)
  {
    OverlayTextDocument document = _text.Document;
    float size = start < end ? document[start].Size : _text.TypingSize(_text.Caret);
    for (int i = start + 1; i < end; i++)
      if (document[i].Size != size || document[i].SizeFormula.Length > 0)
        return false;
    return true;
  }

  // The size in pixels as the game draws it.
  private float ShownSize(int start, int end)
  {
    float size = start < end ? _text.Document[start].Size : _text.TypingSize(_text.Caret);
    return (size > 0 ? size : OverlayElementDefaults.TextSize) * _host.ElementScale;
  }
}
