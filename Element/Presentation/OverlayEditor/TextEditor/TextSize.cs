using System;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.OverlayEditor.Parts.Field;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Property;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The size in the bar: the selection's, or the whole text's with nothing
/// selected, in pixels as the game draws it. The dock types it; a formula
/// sizes it from values, with the plain size kept underneath.
/// </summary>
internal sealed class TextSize
{
  public const float Minimum = 4,
    Maximum = 400;

  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private PropertyButtonViewModel _sizeButton;

  public TextSize(ITextEditorContext context, TextDocumentEditing text, TextFormulas formulas)
  {
    _context = context;
    _text = text;
    _formulas = formulas;
    Slot = new FormulaSlot(TextFormulas.SizeKey, OverlayValueType.Number, Read, Write);
  }

  /// <summary>How the keypad reads and writes the size.</summary>
  public FormulaSlot Slot { get; }

  /// <summary>The bar's Size button; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _sizeButton;

  public PropertyButtonViewModel BuildButton() =>
    _sizeButton = new PropertyButtonViewModel(
      "Text size",
      L("크기", "Size"),
      () => _formulas.Activate(TextFormulas.SizeKey),
      field: OverlayValueType.Number
    );

  // The size shows its result; one worked out from a formula reads in the accent.
  public void Show()
  {
    bool active = _formulas.Edits(TextFormulas.SizeKey, out OverlayVariable typed);
    OverlayVariable formula = typed ?? Read();
    _text.Range(out int start, out int end);
    _sizeButton.ShowField(
      formula,
      formula.IsEmpty ? "" : FormulaFieldView.Number(_formulas.Result(formula, ShownSize(start, end))) + " px",
      active
    );
  }

  private OverlayVariable Read()
  {
    _text.Range(out int start, out int end);
    OverlayTextItem? first = start < end ? _text.Document[start] : null;
    string formula = first?.SizeFormula ?? _text.TypingSizeFormula(_text.Caret);
    if (!string.IsNullOrEmpty(formula))
      return OverlayVariable.Parse(formula);
    return SameSize(start, end)
      ? OverlayVariable.FromNumber(Mathf.Round(ShownSize(start, end) * 10) / 10)
      : new OverlayVariable();
  }

  private void Write(OverlayVariable formula)
  {
    _text.RememberFormula(TextFormulas.SizeKey);
    _text.Range(out int start, out int end);
    float size;
    string text = "";
    if (formula.IsPlainNumber(out double pixels))
      size = Mathf.Clamp((float)pixels, Minimum, Maximum) / _context.ElementScale;
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
    return (size > 0 ? size : OverlayElementDefaults.TextSize) * _context.ElementScale;
  }
}
