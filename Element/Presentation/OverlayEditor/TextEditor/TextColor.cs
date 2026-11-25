using Element.Presentation.OverlayEditor.Parts.Field;
using System;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Variables;
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
/// While the picker is open the dock points at the color's cases
/// (<see cref="ColorCases"/>): "X-Accuracy = 100 then ● Highlight".
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
  private PropertyButtonViewModel _colorButton,
    _opacityButton;
  private Color _shown = Color.white;
  private OverlayResolvedGradient _shownGradient;

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
    OpacitySlot = new FormulaSlot(
      TextFormulas.OpacityKey,
      OverlayValueType.Number,
      ReadOpacity,
      WriteOpacity,
      () => FormulaFieldView.Number(_text.RangeColor().Alpha * 100) + "%"
    );
    Cases = new ColorCases(_picker, _text.RangeColor, WriteColor, _editor.Render);
  }

  /// <summary>The color's cases, which the dock types while the picker is open.</summary>
  public ColorCases Cases { get; }

  /// <summary>The dock points at the color's cases: the picker is open and no number of its is typed.</summary>
  public bool CasesOpen => _picker.Picker != null && _picker.Picker.Visible;

  public UiColorPicker Picker => _picker.Picker;

  public OpacityFieldView Opacity => _picker.Opacity;

  /// <summary>The bar's Text color button; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _colorButton;

  /// <summary>What the open picker colors, as the column's back row names it.</summary>
  public string Title { get; private set; } = "";

  /// <summary>The color the picker belongs to, as the dock names it: the selection's or the text's.</summary>
  public string OwnTitle { get; private set; } = "";

  /// <summary>How the keypad reads and writes the picker's opacity.</summary>
  public FormulaSlot OpacitySlot { get; }

  public PropertyButtonViewModel BuildButton() =>
    _colorButton = new PropertyButtonViewModel(
      "Text color",
      L("글자 색", "Text color"),
      OpenPicker,
      opens: true,
      field: OverlayValueType.Color
    );

  /// <summary>The column's opacity field; made by <see cref="BuildOpacityButton"/>.</summary>
  public PropertyButtonViewModel OpacityButton => _opacityButton;

  public PropertyButtonViewModel BuildOpacityButton() =>
    _opacityButton = new PropertyButtonViewModel(
      "Text opacity",
      L("불투명도", "Opacity"),
      () => _formulas.Activate(TextFormulas.OpacityKey),
      field: OverlayValueType.Number
    );

  /// <summary>The opacity field: what it holds and what that works out to in percent.</summary>
  public void ShowOpacityButton()
  {
    bool active = _formulas.Edits(TextFormulas.OpacityKey, out OverlayVariable typed);
    OverlayVariable alpha = typed ?? ReadOpacity();
    double percent = _formulas.Result(alpha, _text.RangeColor().Alpha * 100);
    _opacityButton.ShowField(alpha, FormulaFieldView.Number(percent) + " %", active);
  }

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

  /// <summary>
  /// After every change: the picker edits a case's color while the formula
  /// being typed ends in one, and the range's own color otherwise.
  /// </summary>
  public void Follow()
  {
    Cases.Follow(_formulas.Editing, ShowRange);
    if (Cases.OnCase)
      Title = ColorCases.CaseTitle;
  }

  // The color the dock made, for the whole range: one undo step with the picker's changes.
  private void WriteColor(OverlayColorSettings color)
  {
    _picker.RememberChange();
    _text.Range(out int start, out int end);
    _text.SetColor(start, end, color);
  }

  // The picker on the range's color: the selection's, or the whole text's.
  private void ShowRange()
  {
    UiColorPicker picker = _picker.Picker;
    Cases.ShowingOwn();
    Title = OwnTitle = _text.HasSelection ? L("선택한 글자 색", "Selected text color") : L("글자 색", "Text color");
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

  public void ShowButton() =>
    _colorButton.ShowField(FieldChips.Of(_text.RangeColor(), _shown, _shownGradient), Picker.Visible);

  /// <summary>The range's color in the bar's button: its swatch, or its gradient.</summary>
  public void ShowSwatch()
  {
    OverlayColorSettings color = _text.RangeColor();
    _swatchFormulas.Compile(color);
    bool gradient = _swatchFormulas.Resolve(color, UiTheme.Accent, _values.WithSamples, 1, 1, _swatchGradient);
    // A condition shows the color it gives for the examples.
    Color shown = OverlayTextComposer.Resolve(
      color,
      UiTheme.Accent,
      null,
      _values.WithSamples,
      OverlayTextComposer.Cases(color),
      out _
    );
    _shown = shown;
    _shownGradient = gradient ? _swatchGradient : null;
  }

  // The picker's opacity shows its result like the size.
  public void ShowOpacity()
  {
    if (!Picker.Visible)
      return;
    bool opacity = _formulas.Edits(TextFormulas.OpacityKey, out OverlayVariable typed);
    OverlayVariable alpha = typed ?? ReadOpacity();
    OverlayColorSettings color = _text.RangeColor();
    Opacity.Show(
      alpha,
      _formulas.Result(alpha, color.Alpha * 100),
      _gradient.PickedStop(color)?.Alpha ?? color.Alpha,
      opacity
    );
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
  private OverlayVariable ReadOpacity() =>
    _gradient.PickedStop(_text.RangeColor()) != null
      ? _gradient.ReadStop(false)
      : OpacityFieldView.Read(_text.RangeColor());

  private void WriteOpacity(OverlayVariable formula)
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
