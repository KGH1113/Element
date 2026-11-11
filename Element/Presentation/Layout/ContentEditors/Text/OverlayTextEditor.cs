using System;
using Element.Application.Input;
using Element.Application.Layout;
using Element.Configuration;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Layout.ContentEditors.Shell;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// Text editing over a dimmed screen. Nothing sits on a card: the text floats
/// in the middle at the size it has in the game, a size, color and opacity
/// bar floats above it. The input dock along the bottom is always there:
/// clicking one of its values adds it at the caret, and clicking a value in
/// the text or a number in the bar types its formula there.
/// </summary>
/// <remarks>
/// The editor puts its parts together and runs them: the text being edited
/// (<see cref="TextDocumentEditing"/>), how it is drawn (<see cref="TextPreview"/>,
/// <see cref="TextLayoutMap"/>, <see cref="TextMarks"/>), what the dock types
/// (<see cref="TextFormulas"/>), the bar's <see cref="TextFonts"/>,
/// <see cref="TextSize"/>, <see cref="TextColor"/> with <see cref="TextGradient"/>
/// and <see cref="TextAnimate"/>, and the input (<see cref="TextValueDrops"/>,
/// <see cref="TextPointer"/>, <see cref="TextKeyboard"/>), with the bar, the
/// grow point and undo it shares with the shape editor.
/// </remarks>
internal sealed class OverlayTextEditor : IContentEditor, ITextEditorHost
{
  private readonly Transform _parent;
  private readonly TMP_FontAsset _uiFont;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayLiveValues _live;
  private readonly OverlayPreviewSpace _space = new();
  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private readonly TextLayoutMap _map;
  private readonly TextMarks _marks;
  private readonly TextFormulas _formulas;
  private readonly TextSize _size;
  private readonly TextGradient _gradient;
  private readonly TextColor _color;
  private readonly TextFonts _fonts;
  private readonly TextAnimate _animate;
  private readonly OverlayGrowFrom _grow;
  private readonly TextValueDrops _drops;
  private readonly TextPointer _pointer;
  private readonly TextKeyboard _keyboard;

  // The values the text, its sizes and opacities, and the dock's formula use.
  private OverlayValueMask _liveMask;
  private OverlayEditorFrame _screen;
  private OverlayEditorBar _bar;
  private OverlayInputDock _dock;
  private TextOverlayElement _element;
  private Action<bool> _closed;
  private float _elementScale = 1;
  private float _bottomTop;
  private float _blinkStart;

  public OverlayTextEditor(Transform parent, TMP_FontAsset uiFont, Func<bool> reduceMotion, OverlayValues values)
  {
    _parent = parent;
    _uiFont = uiFont;
    _reduceMotion = reduceMotion;
    _live = new OverlayLiveValues(values);
    _text = new TextDocumentEditing(values, Render, Restored);
    _preview = new TextPreview(_text, values);
    _map = new TextLayoutMap(_text, _preview);
    _marks = new TextMarks(_text, _preview, _map);
    _formulas = new TextFormulas(this, _text, values);
    _size = new TextSize(this, _text, _formulas);
    _gradient = new TextGradient(this, _text, _formulas, _preview, values);
    _color = new TextColor(this, _text, _formulas, _gradient, values, uiFont, reduceMotion);
    _formulas.Use(_size, _color, _gradient);
    _fonts = new TextFonts(this, _text, _color);
    _animate = new TextAnimate(this, _text, _color, _preview);
    _grow = new OverlayGrowFrom(ClosePopovers, Render);
    _drops = new TextValueDrops(this, _text, _formulas, _map, _marks, _size, _color, _gradient);
    _pointer = new TextPointer(this, _text, _formulas, _map, _color, _fonts, _gradient, _grow, _drops);
    _keyboard = new TextKeyboard(this, _text, _formulas, _map, _preview);
  }

  public bool IsOpen => _screen != null;

  /// <summary>Keeps the editor above the layout toolbar and handles, which raise themselves.</summary>
  public void Raise() => _screen?.Root.transform.SetAsLastSibling();

  OverlayEditorFrame ITextEditorHost.Screen => _screen;

  OverlayInputDock ITextEditorHost.Dock => _dock;

  OverlayEditorBar ITextEditorHost.Bar => _bar;

  TextOverlayElement ITextEditorHost.Element => _element;

  float ITextEditorHost.ElementScale => _elementScale;

  bool ITextEditorHost.AnyPopover => AnyPopover;

  void ITextEditorHost.Render() => Render();

  void ITextEditorHost.ClosePopovers() => ClosePopovers();

  bool ITextEditorHost.InTextArea(Vector2 screen) => InTextArea(screen);

  bool IContentEditor.Open(IOverlayLayoutTarget target, Action<bool> closed)
  {
    if (target is not TextOverlayElement text)
      return false;
    Open(text, closed);
    return true;
  }

  public void Open(TextOverlayElement element, Action<bool> closed)
  {
    Close(false, false);
    _element = element;
    _closed = closed;
    _elementScale = Mathf.Max(.01f, element.EffectiveScale);
    _text.Open(element.Settings?.Text);
    _formulas.Reset();
    _grow.Open(element);
    Build();
    Input.imeCompositionMode = IMECompositionMode.On;
    Render();
  }

  private void Build()
  {
    _color.DisposePicker();
    _screen?.Dispose();
    _drops.Cancel();
    _space.Snap();
    _screen = new OverlayEditorFrame(
      "Text editor",
      _parent,
      _uiFont,
      _reduceMotion,
      L(
        "텍스트 편집 · 값을 눌러 계산 · ⌘/Ctrl+Enter나 Esc로 완료",
        "Edit text · Click a value to calculate · ⌘/Ctrl+Enter or Esc to finish"
      ),
      () => Close(false, true),
      () => Close(true, true)
    );
    _preview.Build(_screen, _element);
    _marks.BuildCarets(_screen.Ui);
    _gradient.BuildHandles(_screen, _reduceMotion());
    BuildBar();
    _bottomTop = _screen.Height - 40;
    _marks.BuildHint(_screen.Ui, _screen.Frame);
    _color.BuildPicker(_screen);
    _gradient.BuildPicker(_screen.Ui, _color.Picker, _reduceMotion());
    _dock = new OverlayInputDock(
      _screen.Ui,
      _screen.Frame,
      _screen.Width,
      _screen.Height,
      _screen.Measure,
      _reduceMotion,
      _formulas.KeypadPress,
      _formulas.ClearFormula,
      _formulas.FinishEditing,
      _formulas.PickValue,
      _drops.BeginValueDrag,
      _drops.MoveValueDrag,
      _drops.EndValueDrag
    );
    _color.LayOutAbove(_dock);
    _fonts.Build(_screen, _element.Fonts, _reduceMotion());
    _grow.Build(_screen, _reduceMotion());
  }

  // One row of labeled buttons: the font, the size, the color (with its
  // opacity), the values' animation and where the text grows from. Font,
  // size, color and animation act on the selection, or on the whole text when
  // nothing is selected.
  private void BuildBar()
  {
    _bar = new OverlayEditorBar(_screen.Ui, _screen.Frame, "Format");
    bool motion = _reduceMotion();
    _bar.Add(_fonts.BuildButton(_screen.Ui, _bar.Root, motion));
    _bar.Add(_size.BuildButton(_screen.Ui, _bar.Root, motion));
    _bar.Add(_color.BuildButton(_screen.Ui, _bar.Root, motion));
    _bar.Add(_animate.BuildButton(_screen.Ui, _bar.Root, motion));
    _bar.Add(_grow.BuildButton(_screen.Ui, _bar.Root, motion));
  }

  public void Tick()
  {
    if (_screen == null)
      return;
    if (_screen.ViewChanged)
    {
      ClosePopovers();
      Build();
      Render();
    }
    _screen.Tick();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible && picker.IsTyping)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
        picker.Hide();
      return;
    }
    _fonts.Tick();
    // The font search takes the keyboard; Esc closes the list.
    if (_fonts.IsTyping)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
      {
        _fonts.Hide();
        Render();
      }
      FollowLiveValues();
      return;
    }
    _pointer.HandleMouse();
    _keyboard.HandleKeys();
    if (_screen == null)
      return;
    _pointer.UpdateHover(Input.mousePosition);
    _keyboard.FollowComposition();
    bool blink = _reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f;
    // The text's caret rests while a formula has the input.
    _marks.Blink(!_text.HasSelection && !_formulas.Editing.Active && blink);
    _dock.Blink(blink);
    _keyboard.UpdateImePosition();
    FollowLiveValues();
    if (_space.Moving)
      RenderKeepingBlink();
  }

  // A level playing behind the editor moves the values it shows.
  private void FollowLiveValues()
  {
    // A font that finished loading (installed fonts are listed in the background) redraws too.
    bool fonts = _element != null && _preview.FontChanged;
    if (!_live.Changed(_liveMask) && !fonts)
      return;
    if (fonts)
      _preview.ApplyFont();
    RenderKeepingBlink();
  }

  private void RenderKeepingBlink()
  {
    float blinkStart = _blinkStart;
    Render();
    _blinkStart = blinkStart;
  }

  private OverlayValueMask LiveMask()
  {
    OverlayFormulaEditing editing = _formulas.Editing;
    OverlayValueMask mask = editing.Active ? editing.Working.Mask : OverlayValueMask.None;
    OverlayTextDocument document = _text.Document;
    for (int i = 0; i < document.Count; i++)
    {
      OverlayTextItem item = document[i];
      if (item.IsValue)
        mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.Value));
      mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.SizeFormula));
      mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.Color.AlphaFormula));
    }
    return mask | _preview.GradientMask;
  }

  // Rebuilds the text at its game size and everything placed around it.
  private void Render()
  {
    if (_screen == null)
      return;
    _blinkStart = Time.unscaledTime;
    _formulas.Sync(_dock);
    // The text stays above the dock.
    _bottomTop = _dock.Top;
    // The text and its bar keep to the space left of an open picker; it
    // glides aside as the picker opens, and back as it closes.
    _space.Measure(_screen, _color.Picker, _dock, _reduceMotion(), out float left, out float right);
    _preview.Draw(left, right, OverlayEditorFrame.TopBar + 64, _bottomTop - 40, _screen.Scale, _elementScale);
    _map.Map();
    _marks.PlaceCaret();
    _marks.Place(_drops.InsertIndex, _drops.Joining, _formulas.EditedValue);
    ShowFields();
    float barWidth = _bar.LayOut();
    _bar.Place(
      Mathf.Max(OverlayEditorFrame.Margin, (left + right) * .5f - barWidth * .5f),
      Mathf.Max(OverlayEditorFrame.TopBar, _preview.Top - OverlayEditorBar.Height - 18),
      barWidth
    );
    _color.ShowSwatch();
    _color.Place(_dock);
    _gradient.Show();
    _fonts.Place(_dock);
    _formulas.Refresh(_dock);
    PlaceGrowMark();
    _marks.PlaceEmptyHint(_preview.Center, _preview.Empty, _screen.Scale);
    _liveMask = LiveMask();
  }

  // The bar's buttons show the range's font, size, color, animation and grow point.
  private void ShowFields()
  {
    _size.Show();
    _color.ShowButton();
    _animate.Show();
    _fonts.ShowButton();
    _grow.ShowButton();
    _color.ShowOpacity();
  }

  // While the grow popover is open, a dot marks the point that stays put.
  private void PlaceGrowMark()
  {
    if (!_grow.Visible)
    {
      _grow.HideMark();
      return;
    }
    Rect text = _screen.FrameRect(_preview.Text.rectTransform);
    _grow.PlaceMark(text.x, text.y, text.width, text.height);
  }

  private bool AnyPopover => _formulas.Open || _grow.Visible || _fonts.Visible;

  private void ClosePopovers()
  {
    _formulas.Close();
    _dock?.Edit(null);
    _grow.Hide();
    _fonts.Hide();
  }

  // The dimmed space around the text belongs to it; the top bar, the format
  // bar, the popovers and the picker don't.
  private bool InTextArea(Vector2 screen)
  {
    if (
      _dock.Contains(screen)
      || _grow.Contains(screen)
      || _bar.Contains(screen)
      || PopoverPlacement.Contains(_color.Picker.Root, screen)
      || _fonts.Contains(screen)
    )
      return false;
    float y = _screen.ToFrame(screen).y;
    return y >= OverlayEditorFrame.TopBar && y < _bottomTop - 18;
  }

  // Undo or redo put an earlier text back; the keypad reads the restored formula afresh.
  private void Restored()
  {
    _formulas.Editing.Deactivate();
    Render();
  }

  /// <summary>Esc closes the picker first, then finishes like Done; only Cancel leaves without saving.</summary>
  public void Escape()
  {
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    else
      Close(true, true);
  }

  /// <summary>Done writes the runs to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_screen == null)
      return;
    if (commit && _color.Picker.Visible && !_color.Picker.Commit())
      return;
    ClosePopovers();
    TextOverlayElement element = _element;
    Action<bool> closed = _closed;
    if (commit && element?.Settings != null && _text.Edited)
    {
      element.Settings.Text ??= new OverlayTextSettings();
      element.Settings.Text.Runs = _text.Document.ToRuns();
      element.Apply();
    }
    // Where it grows from is part of its placement, saved with the text.
    if (commit && element?.Settings != null)
      element.SetGrowFrom(_grow.Growth);
    Input.imeCompositionMode = IMECompositionMode.Auto;
    _color.DisposePicker();
    _screen.Dispose();
    _screen = null;
    _drops.Cancel();
    _formulas.Editing.Deactivate();
    _element = null;
    _closed = null;
    InputGate.SetTextEditing(false);
    if (notify)
      closed?.Invoke(commit);
  }

  // Torn down with the editor open (the mod turned off), the edit is kept;
  // a color that can't be read is the only thing that falls back.
  public void Dispose()
  {
    Close(true, false);
    Close(false, false);
  }
}
