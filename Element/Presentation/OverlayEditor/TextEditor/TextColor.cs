using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Formula;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.ColorPicker;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Theme;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The text's color: the bar's button with a swatch of the selection's
/// color (or the whole text's), and the color picker right above the
/// keypad, which carries the color's opacity as a number the dock types.
/// </summary>
internal sealed class TextColor
{
  private readonly IEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextGradient _gradient;
  private readonly OverlayValues _values;
  private readonly ColorPickerPanel _picker;
  private readonly OverlayGradientFormulas _swatchFormulas = new();
  private readonly OverlayResolvedGradient _swatchGradient = new();
  private PropertyButtonViewModel _colorButton;

  public TextColor(
    IEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextGradient gradient,
    OverlayValues values,
    TMP_FontAsset font,
    Func<bool> reduceMotion
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _formulas = formulas;
    _gradient = gradient;
    _values = values;
    _picker = new ColorPickerPanel(
      font,
      reduceMotion,
      () => _text.Remember(false),
      () => _context.Frame != null,
      // Its numbers go with it.
      () => _formulas.Field == TextFormulas.OpacityKey || TextGradient.IsKey(_formulas.Field),
      _editor.CloseOpen,
      _editor.Render
    );
    OpacitySlot = new FormulaSlot(TextFormulas.OpacityKey, false, ReadOpacity, WriteOpacity);
  }

  public UiColorPicker Picker => _picker.Picker;

  public OpacityFieldView Opacity => _picker.Opacity;

  /// <summary>The bar's Text color button; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _colorButton;

  /// <summary>What the open picker colors, as the column's back row names it.</summary>
  public string Title { get; private set; } = "";

  /// <summary>How the keypad reads and writes the picker's opacity.</summary>
  public FormulaSlot OpacitySlot { get; }

  public PropertyButtonViewModel BuildButton() =>
    _colorButton = new PropertyButtonViewModel(
      "Text color",
      L("글자 색", "Text color"),
      OpenPicker,
      value: true,
      swatch: true,
      opens: true
    );

  /// <summary>The picker over the controls, with its opacity as a number.</summary>
  public void BuildPicker(EditorFramePanel screen)
  {
    _picker.Build(screen.Frame);
    _picker.BuildOpacity(screen.Ui, () => _formulas.Activate(TextFormulas.OpacityKey));
  }

  /// <summary>The color picker opens right above the keypad, in its column.</summary>
  public void LayOutAbove(InputDockPanel dock) => _picker.LayOutAbove(dock);

  public void DisposePicker() => _picker.Dispose();

  private void OpenPicker()
  {
    _editor.CloseOpen();
    UiColorPicker picker = _picker.Picker;
    if (picker.Visible)
    {
      picker.Hide();
      _editor.Render();
      return;
    }
    ShowRange();
    _editor.Render();
  }

  /// <summary>
  /// The open picker goes on with a new selection, or with the whole text
  /// once nothing is selected: it shows that color, and its first change is
  /// a new undo step.
  /// </summary>
  public void Retarget()
  {
    UiColorPicker picker = _picker.Picker;
    if (picker == null || !picker.Visible)
      return;
    _picker.NewSession();
    ShowRange();
  }

  // The picker on the range's color: the selection's, or the whole text's.
  private void ShowRange()
  {
    UiColorPicker picker = _picker.Picker;
    Title = _text.HasSelection ? L("선택한 글자 색", "Selected text color") : L("글자 색", "Text color");
    picker.ExternalOpacity = true;
    picker.ShowSource(
      Title,
      _text.RangeColor(),
      color =>
      {
        _text.Range(out int start, out int end);
        _text.SetColor(
          start,
          end,
          _picker.Changed(_text.RangeColor(), color, _formulas.Field == TextFormulas.OpacityKey, _formulas.Editing)
        );
        _editor.Render();
      }
    );
  }

  /// <summary>The picker in the property column from the tool's top, above the keypad.</summary>
  public void Place(InputDockPanel dock, InspectorPanel inspector) => _picker.Place(dock, inspector.ToolTop);

  public void ShowButton() => _colorButton.Set(ColorNames.Describe(_text.RangeColor()), false, Picker.Visible);

  /// <summary>The range's color in the bar's button: its swatch, or its gradient.</summary>
  public void ShowSwatch()
  {
    OverlayColorSettings color = _text.RangeColor();
    _swatchFormulas.Compile(color);
    bool gradient = _swatchFormulas.Resolve(color, UiTheme.Accent, _values.WithSamples, 1, 1, _swatchGradient);
    _colorButton.SetSwatch(OverlayTextComposer.Resolve(color, UiTheme.Accent), gradient ? _swatchGradient : null);
  }

  // The picker's opacity shows its result like the size.
  public void ShowOpacity()
  {
    if (!Picker.Visible)
      return;
    bool opacity = _formulas.Edits(TextFormulas.OpacityKey, out OverlayFormula typed);
    OverlayFormula alpha = typed ?? ReadOpacity();
    OverlayColorSettings color = _text.RangeColor();
    Opacity.Show(alpha, _formulas.Result(alpha), _gradient.PickedStop(color)?.Alpha ?? color.Alpha, opacity);
  }

  /// <summary>The line in the dock while the picker is open; null while it is closed.</summary>
  public string Hint() =>
    Picker.Visible && Picker.Gradient.IsGradient
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도·위치·각도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on an opacity, position or angle to calculate it"
      )
    : Picker.Visible
      ? L(
        "글자 색을 고르는 중이에요 · 값을 불투명도 칸에 끌어 놓으면 그 값으로 계산해요",
        "Picking the text color · Drop a value on the opacity to calculate it"
      )
    : null;

  // A picked gradient color has its own opacity.
  private OverlayFormula ReadOpacity() =>
    _gradient.PickedStop(_text.RangeColor()) != null
      ? _gradient.ReadStop(false)
      : OpacityFieldView.Read(_text.RangeColor());

  private void WriteOpacity(OverlayFormula formula)
  {
    if (_gradient.PickedStop(_text.RangeColor()) != null)
    {
      _gradient.WriteStop(false, formula);
      return;
    }
    _text.RememberFormula(TextFormulas.OpacityKey);
    _text.Range(out int start, out int end);
    OverlayColorSettings color = OpacityFieldView.Write(_text.RangeColor(), formula);
    if (start < end)
      _text.Document.SetAlpha(start, end, color.Alpha, color.AlphaFormula);
    else
      _text.PendingColor = color;
  }
}
