using System;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Values;
using Element.Domain.Overlay.Variables;
using Element.Presentation.Overlay.Text;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.Parts.ColorPicker;

/// <summary>
/// A color as a variable: one color (picked by hand, or the highlight
/// color), a condition giving colors, or a value of the player's. The dock
/// opens it like any field (<see cref="Slot"/>) while its picker is open,
/// and the picker is the input method of the color piece picked in the
/// dock: it shows that color and its changes go to it. With nothing but a
/// gradient, the picker edits the gradient itself.
/// </summary>
internal sealed class ColorCases
{
  /// <summary>The dock's key for a color.</summary>
  public const string Key = "color";

  private static readonly StringBuilder Builder = new(48);

  private readonly ColorPickerPanel _picker;
  private readonly Func<OverlayColorSettings> _color;
  private readonly Action<OverlayColorSettings> _write;
  private readonly Action _render;
  private OverlayPiece _shown;
  private int _shownDepth = -1;
  private bool _onCase;

  /// <param name="color">The color edited: the selection's, the fill or the border.</param>
  /// <param name="write">Stores a new color; the owner keeps the change for undo.</param>
  public ColorCases(
    ColorPickerPanel picker,
    Func<OverlayColorSettings> color,
    Action<OverlayColorSettings> write,
    Action render
  )
  {
    _picker = picker;
    _color = color;
    _write = write;
    _render = render;
    Slot = new FormulaSlot(Key, OverlayValueType.Color, Read, Write, Placeholder);
  }

  /// <summary>How the dock reads and writes the color.</summary>
  public FormulaSlot Slot { get; }

  /// <summary>The picker shows a color piece inside a condition, not the color as a whole.</summary>
  public bool OnCase => _onCase;

  /// <summary>What the column's back row says while the picker shows a color inside a condition.</summary>
  public static string CaseTitle => L("조건 안의 색", "Color in the condition");

  /// <summary>The owner showed the color as a whole in the picker again.</summary>
  public void ShowingOwn()
  {
    _onCase = false;
    _shown = null;
    _shownDepth = -1;
    if (_picker.Picker != null)
      _picker.Picker.ShowsOther = false;
  }

  /// <summary>
  /// After every change: the picker shows the color piece picked in the dock
  /// and edits it. A color that is one piece, or a gradient, is edited as a
  /// whole (<paramref name="showOwn"/>); with something else picked (a
  /// condition), the picker waits, saying how to pick a color.
  /// </summary>
  public void Follow(FormulaEditing editing, Action showOwn)
  {
    UiColorPicker picker = _picker.Picker;
    if (picker == null || !picker.Visible || !editing.Active || editing.Slot?.Key != Key)
      return;
    OverlayPiece piece = editing.SelectedPiece;
    int depth = editing.Path.Count;
    bool whole = depth == 1 && (editing.Root.Count == 0 || (editing.Root.Count == 1 && IsColor(editing.Root[0])));
    if (whole)
    {
      if (_onCase || _shown != null)
      {
        ShowingOwn();
        showOwn();
      }
      return;
    }
    if (piece != null && IsColor(piece))
    {
      if (!_onCase || _shown == null || !_shown.SameAs(piece) || _shownDepth != depth)
        ShowPiece(editing, piece, depth);
      return;
    }
    if (_shown != null || !_onCase)
      ShowWaiting();
  }

  // The picker on one color piece: highlight or custom, solid, with the color's own opacity.
  private void ShowPiece(FormulaEditing editing, OverlayPiece piece, int depth)
  {
    _onCase = true;
    _shown = piece.Clone();
    _shownDepth = depth;
    float alpha = _color()?.Alpha ?? 1;
    OverlayColorSettings shown =
      piece.Kind == OverlayPieceKind.Color
        ? OverlayColorSettings.Custom(piece.Text, alpha)
        : OverlayColorSettings.HighlightColor(alpha);
    _picker.NewSession();
    _picker.Picker.ShowsOther = true;
    _picker.Picker.Model.WaitNote = null;
    _picker.Picker.ShowSource(
      CaseTitle,
      shown,
      picked =>
      {
        OverlayPiece given = picked.Highlight
          ? OverlayPiece.ValuePiece(OverlayValueId.HighlightColor)
          : OverlayPiece.ColorPiece(picked.Hex ?? "#FFFFFF");
        _shown = given.Clone();
        editing.SetColor(given);
        _render();
      },
      gradient: false
    );
  }

  // Nothing to pick a color for: the picker says how to get one.
  private void ShowWaiting()
  {
    _onCase = true;
    _shown = null;
    _shownDepth = -1;
    _picker.Picker.ShowsOther = true;
    _picker.Picker.Model.WaitNote = L(
      "색 조각을 고르면 여기서 골라요 · 조건이면 결과값이나 기본값 안으로 들어가요",
      "Pick a color piece to choose it here · In a condition, go into its result or default"
    );
    _picker.Picker.ShowSource(
      CaseTitle,
      OverlayColorSettings.HighlightColor(_color()?.Alpha ?? 1),
      _ => { },
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
    // The line shows it as its swatch beside its code.
    return "#" + ColorUtility.ToHtmlStringRGB(shown);
  }

  // The color as a variable: its own pieces, or its plain color as one piece; nothing for a gradient.
  private OverlayVariable Read()
  {
    OverlayColorSettings color = _color();
    if (color == null)
      return new OverlayVariable();
    if (!string.IsNullOrEmpty(color.Formula))
      return OverlayVariable.Parse(color.Formula);
    if (color.IsGradient)
      return new OverlayVariable();
    return new OverlayVariable(
      color.Highlight ? OverlayPiece.ValuePiece(OverlayValueId.HighlightColor) : OverlayPiece.ColorPiece(color.Hex)
    );
  }

  // One color piece is the plain color again; anything more keeps the plain color underneath.
  private void Write(OverlayVariable variable)
  {
    OverlayColorSettings color = (_color() ?? new OverlayColorSettings()).Clone();
    if (variable.Count == 1 && IsColor(variable[0]))
    {
      color.Formula = string.Empty;
      color.Gradient = null;
      color.Highlight = variable[0].Kind == OverlayPieceKind.Value;
      if (!color.Highlight)
        color.Hex = variable[0].Text;
    }
    else
      color.Formula = variable.Serialize();
    _write(color);
  }

  private string Placeholder()
  {
    OverlayColorSettings color = _color();
    if (color == null || !color.IsGradient)
      return null;
    Builder.Clear().Append("<mark=#FFFFFF26> ");
    InputDockViewModel.Swatch(Builder, OverlayTextComposer.Resolve(color, UiTheme.Accent));
    return Builder.Append(ColorNames.Describe(color)).Append(" </mark>").ToString();
  }

  private static bool IsColor(OverlayPiece piece) =>
    piece.Kind == OverlayPieceKind.Color
    || (piece.Kind == OverlayPieceKind.Value && OverlayValueCatalog.Get(piece.Value).IsColor);
}
