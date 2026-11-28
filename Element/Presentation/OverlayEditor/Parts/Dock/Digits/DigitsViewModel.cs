using System;
using System.Collections.Generic;
using System.Text;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Values;
using ElementUI;

namespace Element.Presentation.OverlayEditor.Parts.Dock.Digits;

/// <summary>
/// The input method of a value with decimals (progress, X-Accuracy, BPM):
/// its own number, written out to four decimals. As far as it is read here
/// the number is solid, past that faint; a digit pressed is where it ends
/// (the whole part or the point: whole). That number is what a condition
/// compares and a text writes, cut the way the game writes that value.
/// Under the pointer the number shows what it would read. Under it, the
/// operators that carry on a calculation.
/// </summary>
internal sealed class DigitsViewModel : ViewModel
{
  private readonly StringBuilder _cut = new(16),
    _fine = new(16);
  private OverlayValueDefinition _value;
  private double _raw = double.NaN;
  private int _read,
    _hovered = -1;

  /// <param name="pick">Reads the picked value at that many decimals.</param>
  public DigitsViewModel(Action<int> pick)
  {
    DigitsGlyphViewModel[] glyphs = new DigitsGlyphViewModel[OverlayValueNumbers.Most + 2];
    glyphs[0] = Own(new DigitsGlyphViewModel(0, "Digits 0", pick, Hover));
    glyphs[1] = Own(new DigitsGlyphViewModel(0, "Digits point", pick, Hover));
    for (int digits = 1; digits <= OverlayValueNumbers.Most; digits++)
      glyphs[digits + 1] = Own(new DigitsGlyphViewModel(digits, "Digits " + digits, pick, Hover));
    Glyphs = glyphs;
  }

  /// <summary>The whole part, the point, then one to four decimals.</summary>
  public readonly IReadOnlyList<DigitsGlyphViewModel> Glyphs;

  /// <summary>How many decimals the value is read at here.</summary>
  public int Read => _read;

  /// <summary>
  /// Shows <paramref name="value"/>'s number as <paramref name="numbers"/>
  /// have it now (the game's, or the value's example), solid as far as it is read.
  /// </summary>
  public void Show(OverlayPiece value, IOverlayNumberSource numbers)
  {
    if (value == null || value.Kind != OverlayPieceKind.Value)
      return;
    _value = OverlayValueCatalog.Get(value.Value);
    _raw = double.NaN;
    numbers?.TryGetNumber(value.Value, out _raw);
    _read = OverlayValueNumbers.DigitsOf(value, _value);
    Paint();
  }

  // The pointer over a part shows the number read up to it; off it, as it is read.
  private void Hover(int digits, bool over)
  {
    if (over)
      _hovered = digits;
    else if (_hovered == digits)
      _hovered = -1;
    Paint();
  }

  private void Paint()
  {
    if (_value == null)
      return;
    int shown = _hovered >= 0 ? _hovered : _read;
    if (double.IsNaN(_raw))
    {
      foreach (DigitsGlyphViewModel glyph in Glyphs)
      {
        glyph.Text.Value = glyph.Name == "Digits 0" ? "—" : "";
        glyph.Solid.Value = true;
        glyph.Hot.Value = false;
      }
      return;
    }
    // Solid: the number at that many decimals, cut the game's way. Faint: what more decimals would add.
    _cut.Clear().AppendScaled(OverlayValueNumbers.Scaled(_raw, shown, _value.Rounding), shown, true);
    _fine
      .Clear()
      .AppendScaled(
        OverlayValueNumbers.Scaled(_raw, OverlayValueNumbers.Most, _value.Rounding),
        OverlayValueNumbers.Most,
        true
      );
    int cutPoint = IndexOf(_cut, '.'),
      finePoint = IndexOf(_fine, '.');
    Glyphs[0].Text.Value = _cut.ToString(0, cutPoint < 0 ? _cut.Length : cutPoint);
    Glyphs[0].Solid.Value = true;
    Glyphs[0].Hot.Value = false;
    Glyphs[1].Text.Value = ".";
    Glyphs[1].Solid.Value = shown > 0;
    Glyphs[1].Hot.Value = false;
    for (int digits = 1; digits <= OverlayValueNumbers.Most; digits++)
    {
      DigitsGlyphViewModel glyph = Glyphs[digits + 1];
      bool solid = digits <= shown;
      glyph.Text.Value = (solid ? _cut[cutPoint + digits] : _fine[finePoint + digits]).ToString();
      glyph.Solid.Value = solid;
      glyph.Hot.Value = _hovered >= 0 && _hovered != _read && digits == shown;
    }
  }

  private static int IndexOf(StringBuilder text, char c)
  {
    for (int i = 0; i < text.Length; i++)
      if (text[i] == c)
        return i;
    return -1;
  }
}
