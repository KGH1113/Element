using System;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>
/// A color's cases, typed in the input dock while its picker is open:
/// "X-Accuracy = 100 then ● Highlight". The dock points at the color as it
/// does at a number (<see cref="Slot"/>), and what shows when no case holds
/// is the color's own. The picker edits the color a case just got, while
/// the formula being typed ends in it, and the color's own the rest of the
/// time (after Apply, too); a color value picked with no case written
/// becomes the color's own.
/// </summary>
internal sealed class ColorCases
{
  /// <summary>The dock's key for a color's cases.</summary>
  public const string Key = "color";

  private static readonly StringBuilder Builder = new(48);

  private readonly ColorPickerPanel _picker;
  private readonly Func<OverlayColorSettings> _color;
  private readonly Action<string> _write;
  private readonly Action _render;
  private bool _onCase;
  private OverlayFormulaToken _shown;

  /// <param name="color">The color edited: the selection's, the fill or the border.</param>
  /// <param name="write">Saves the cases, as saved text; empty for none. The owner keeps the change for undo.</param>
  public ColorCases(ColorPickerPanel picker, Func<OverlayColorSettings> color, Action<string> write, Action render)
  {
    _picker = picker;
    _color = color;
    _write = write;
    _render = render;
    Slot = new FormulaSlot(Key, OverlayFormulaKind.Color, Read, Write, Own, OwnColor);
  }

  /// <summary>How the dock reads and writes the cases.</summary>
  public FormulaSlot Slot { get; }

  /// <summary>The picker shows the color a case just got, not the color's own.</summary>
  public bool OnCase => _onCase;

  /// <summary>What the column's back row says while the picker shows a case's color.</summary>
  public static string CaseTitle => L("조건이 맞을 때 색", "Color when it holds");

  /// <summary>The owner showed the color's own in the picker again.</summary>
  public void ShowingOwn()
  {
    _onCase = false;
    if (_picker.Picker != null)
      _picker.Picker.ShowsOther = false;
  }

  /// <summary>
  /// After every change: while the formula being typed ends in a case's
  /// color, the picker edits that color; once it doesn't, the color's own
  /// again (<paramref name="showOwn"/>).
  /// </summary>
  public void Follow(FormulaEditing editing, Action showOwn)
  {
    UiColorPicker picker = _picker.Picker;
    OverlayFormulaToken color = default;
    bool onCase =
      picker != null
      && picker.Visible
      && editing.Active
      && editing.Slot?.Key == Key
      && editing.Typed
      && editing.EndsInColor(out color);
    if (onCase)
    {
      if (!_onCase || !_shown.Same(color))
        ShowCase(editing, color);
      return;
    }
    if (_onCase)
    {
      _onCase = false;
      showOwn();
    }
  }

  // The picker on the color a case just got: highlight or custom, solid, with the color's own opacity.
  private void ShowCase(FormulaEditing editing, OverlayFormulaToken color)
  {
    _onCase = true;
    _shown = color;
    float alpha = _color()?.Alpha ?? 1;
    OverlayColorSettings shown =
      color.Kind == OverlayFormulaTokenKind.Color
        ? OverlayColorSettings.Custom(color.Hex, alpha)
        : OverlayColorSettings.HighlightColor(alpha);
    _picker.NewSession();
    _picker.Picker.ShowsOther = true;
    _picker.Picker.ShowSource(
      CaseTitle,
      shown,
      picked =>
      {
        OverlayFormulaToken given = picked.Highlight
          ? OverlayFormulaToken.ValueToken(OverlayValueId.HighlightColor)
          : OverlayFormulaToken.ColorToken((picked.Hex ?? "#FFFFFF").ToUpperInvariant());
        _shown = given;
        editing.SetEndColor(given);
        _render();
      },
      gradient: false
    );
  }

  /// <summary>The color shown now, as a swatch for the dock's result.</summary>
  public string Result(IOverlayNumberSource samples)
  {
    OverlayColorSettings color = _color();
    Color shown = OverlayTextComposer.Resolve(
      color,
      UiTheme.Accent,
      null,
      samples,
      OverlayTextComposer.Cases(color),
      out _
    );
    return InputDockViewModel.Swatch(Builder.Clear(), shown).ToString().TrimEnd();
  }

  private OverlayFormula Read() => OverlayFormula.Parse(_color()?.Formula);

  private void Write(OverlayFormula formula) => _write(formula.HasCases ? formula.Serialize() : string.Empty);

  // The color's own, as the dock names it after the cases: its swatch and what it is.
  private string Own()
  {
    OverlayColorSettings color = _color();
    if (color == null)
      return "";
    Builder.Clear().Append("<mark=#FFFFFF26> ");
    InputDockViewModel.Swatch(Builder, OverlayTextComposer.Resolve(color, UiTheme.Accent));
    Builder.Append(ColorNames.Describe(color)).Append(" </mark>");
    return Builder.ToString();
  }

  // A color value picked with no case written: the highlight color, or a custom one.
  private void OwnColor(OverlayValueDefinition value)
  {
    UiColorPicker picker = _picker.Picker;
    if (picker == null || !picker.Visible || _onCase)
      return;
    picker.Model.SetSource(value.Id == OverlayValueId.HighlightColor);
    _render();
  }
}
