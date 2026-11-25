using System;
using System.Collections.Generic;
using Element.Application.Input;
using Element.Application.Layout;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.Dock;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.Grow;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Popovers;
using ElementUI.Ugui;
using TMPro;
using UnityEngine;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// Text editing over a dimmed screen. Nothing sits on a card: the text floats
/// at the size it has in the game, left of a property column (font, size,
/// color, animation, grow point) that stands above the keypad; what a row
/// opens (the color picker, the font list, the points) opens in that column.
/// The input dock along the bottom is always there: clicking one of its
/// values adds it at the caret, and clicking a value in the text or a number
/// in the column types its formula there.
/// </summary>
/// <remarks>
/// The editor puts its parts together and runs them: the text being edited
/// (<see cref="TextDocumentEditing"/>), how it is drawn (<see cref="TextPreview"/>,
/// <see cref="TextLayoutMap"/>, <see cref="TextMarks"/>), what the dock types
/// (<see cref="TextFormulas"/>), the bar's <see cref="TextFonts"/>,
/// <see cref="TextSize"/>, <see cref="TextColor"/> with <see cref="TextGradient"/>
/// and <see cref="TextAnimate"/>, and the input (<see cref="TextValueDrops"/>,
/// <see cref="TextPointerInput"/>, <see cref="TextKeyboardInput"/>), with the bar, the
/// grow point and undo it shares with the shape editor. It tells the input
/// dock what is edited on every render (<see cref="TextFormulas.Refresh"/>),
/// and everything done in the dock comes back here, through
/// <see cref="IInputDockActions"/>.
/// </remarks>
internal sealed class TextEditorScreen : IContentEditor, ITextEditorContext, ITextEditorActions, IInputDockActions
{
  private readonly Transform _parent;
  private readonly TMP_FontAsset _uiFont;
  private readonly Func<bool> _reduceMotion;
  private readonly OverlayValues _values;
  private readonly EditorLiveValues _live;
  private readonly EditorPreviewSpace _space = new();
  private readonly PreviewZoom _zoom = new();
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
  private readonly GrowFrom _grow;

  // The grow-from point and the font list; Esc and a press elsewhere close them.
  private readonly PopoverStack _popovers = new();
  private readonly TextValueDrops _drops;
  private readonly TextPointerInput _pointer;
  private readonly TextKeyboardInput _keyboard;

  // The values the text, its sizes and opacities, and the dock's formula use.
  private OverlayValueMask _liveMask;
  private EditorFramePanel _frame;
  private InspectorPanel _inspector;
  private InputDockViewModel _dock;
  private InputDockPanel _dockPanel;
  private TextOverlayElement _element;
  private Action<bool> _closed;
  private float _elementScale = 1;
  private float _bottomTop;
  private float _blinkStart;
  private bool _dockTyping;

  // What the column's tool acts on, as last drawn: the selection's ends, or -1 for the whole text.
  private int _toolStart = -1,
    _toolEnd = -1;

  public TextEditorScreen(
    Transform parent,
    TMP_FontAsset uiFont,
    Func<bool> reduceMotion,
    OverlayValues values,
    ICustomValueBook book
  )
  {
    _parent = parent;
    _uiFont = uiFont;
    _reduceMotion = reduceMotion;
    _values = values;
    _live = new EditorLiveValues(values);
    _text = new TextDocumentEditing(values, Render, Restored);
    _preview = new TextPreview(_text, values);
    _map = new TextLayoutMap(_text, _preview);
    _marks = new TextMarks(_text, _preview, _map);
    // Each part gets this screen twice: as what it reads (the context) and as
    // what it asks of it (the actions).
    _formulas = new TextFormulas(this, this, _text, values, book);
    _size = new TextSize(this, _text, _formulas);
    _gradient = new TextGradient(this, this, _text, _formulas, _preview, values);
    _color = new TextColor(this, this, _text, _formulas, _gradient, values, uiFont, reduceMotion);
    _formulas.Use(_size, _color, _gradient);
    _fonts = new TextFonts(this, this, _text, _color);
    _animate = new TextAnimate(this, _text, _color, _preview);
    _grow = new GrowFrom(CloseOpen, Render);
    _drops = new TextValueDrops(this, this, _text, _formulas, _map, _marks, _color, _gradient);
    _pointer = new TextPointerInput(this, this, _text, _formulas, _map, _color, _fonts, _gradient, _grow, _animate);
    _keyboard = new TextKeyboardInput(this, this, _text, _formulas, _map, _preview);
  }

  public bool IsOpen => _frame != null;

  /// <summary>Keeps the editor above the layout toolbar and handles, which raise themselves.</summary>
  public void Raise() => _frame?.Root.transform.SetAsLastSibling();

  // ---- What the parts read: where things are and what is edited ----

  EditorFramePanel IEditorContext.Frame => _frame;

  InputDockViewModel IEditorContext.Dock => _dock;

  InputDockPanel IEditorContext.DockPanel => _dockPanel;

  InspectorPanel IEditorContext.Inspector => _inspector;

  TextOverlayElement ITextEditorContext.Element => _element;

  float IEditorContext.ElementScale => _elementScale;

  bool IEditorContext.AnyOpen => AnyOpen;

  bool ITextEditorContext.InTextArea(Vector2 screen) => InTextArea(screen);

  // ---- What the parts ask for: a change made, they ask for the screen to be drawn again ----

  void IEditorActions.Render() => Render();

  void IEditorActions.CloseOpen() => CloseOpen();

  void ITextEditorActions.CloseTyping() => CloseTyping();

  void IEditorActions.Notice(string message, bool warning) => _frame?.ViewModel.Notice(message, warning);

  // ---- What the dock asks for: everything done in the dock lands here ----

  void IInputDockActions.Press(char key) => _formulas.KeypadPress(key);

  void IInputDockActions.SelectAt(IReadOnlyList<FormulaStep> route, int index) =>
    Edit(editing => editing.SelectAt(route, index));

  void IInputDockActions.Words(string words) => Edit(editing => editing.SetText(words));

  void IInputDockActions.Switch(bool value) => Edit(editing => editing.SetBool(value));

  void IInputDockActions.Rename(string name) => Edit(editing => editing.Rename(name));

  // A change the dock asked for, made to what it has open, then drawn.
  private void Edit(System.Action<FormulaEditing> change)
  {
    if (_frame == null || !_formulas.Editing.Active)
      return;
    change(_formulas.Editing);
    Render();
  }

  void IInputDockActions.Remove() => Edit(editing => editing.Remove());

  void IInputDockActions.Done() => _formulas.FinishEditing();

  // The clicked value joins the formula being typed, or goes in at the caret.
  void IInputDockActions.Pick(OverlayValueDefinition value) => _formulas.PickValue(value);

  // A value's name tip would sit over where the carried value lands.
  void IInputDockActions.BeganCarrying() => _frame?.HideTip();

  // Let go on the dock's line: the value joins the formula, or words say why they can't.
  void IInputDockActions.DropOnFormula(OverlayValueDefinition value)
  {
    FormulaEditing editing = _formulas.Editing;
    if (editing.Active && editing.Drop(editing.Slot, value) == FormulaDropResult.Refused)
      _formulas.Refuse(value);
    Render();
  }

  // ---- Opening, running and closing ----

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
    _toolStart = _toolEnd = -1;
    Build();
    Input.imeCompositionMode = IMECompositionMode.On;
    Render();
  }

  private void Build()
  {
    _color.DisposePicker();
    _frame?.Dispose();
    _drops.Release();
    _space.Snap();
    _zoom.Snap();
    _frame = new EditorFramePanel(
      "Text editor",
      _parent,
      _uiFont,
      _reduceMotion,
      L("텍스트 편집", "Edit text"),
      () => Close(false, true),
      () => Close(true, true),
      noticesInDock: true
    );
    _preview.Build(_frame, _element);
    _zoom.Build(_frame.Ui, _frame.Frame);
    _marks.BuildCarets(_frame.Ui);
    _gradient.BuildHandles(_frame, _reduceMotion());
    _bottomTop = _frame.Height - 40;
    _marks.BuildHint(_frame.Ui, _frame.Frame);
    _color.BuildPicker(_frame);
    _gradient.BuildPicker(_frame.Ui, _color.Picker, _reduceMotion());
    _dock = new InputDockViewModel(_values, _formulas.Editing, _frame.ViewModel, this, _reduceMotion());
    _dockPanel = new InputDockPanel(_frame, _dock, _reduceMotion());
    _color.LayOutAbove(_dockPanel);
    BuildInspector();
    _fonts.Build(_frame, _element.Fonts, _reduceMotion());
    _grow.Build(_frame, _inspector);
    _animate.Build(_frame, _inspector);
    _popovers.Clear();
    _popovers.Add(_grow.Popover);
    _popovers.Add(_fonts.Popover);
    // Built last: every place a carried value can land is drawn by now.
    _drops.Build(_frame);
  }

  // A row per property in the column above the keypad: the font, the size,
  // the color (with its opacity), the values' animation and where the text
  // grows from. Font, size, color and animation act on the selection, or on
  // the whole text when nothing is selected.
  private void BuildInspector() =>
    _inspector = new InspectorPanel(
      _frame,
      _dockPanel,
      "Format",
      L("글자", "Text"),
      CloseTool,
      _fonts.BuildButton(),
      // A value carried onto the size row calculates the size with it.
      _size.BuildButton(_drops.DropOnSize),
      _color.BuildButton(),
      _animate.BuildButton(),
      _grow.BuildButton()
    );

  // The column's back row: the rows come back, with whatever the tool had open.
  private void CloseTool()
  {
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    _animate.Hide();
    CloseOpen();
    Render();
  }

  // Where the open tool is drawn; its back row lines up with it. A picker
  // the Animate tool opened beside the column belongs to that tool.
  private RectTransform OpenToolRoot() =>
    _animate.Visible ? _animate.Root
    : _color.Picker.Visible ? _color.Picker.Root
    : _fonts.Visible ? _fonts.Popover.Root
    : _grow.Visible ? _grow.Popover.Root
    : null;

  // What the column has open, as its back row names it; null while it shows the rows.
  private string OpenTool() =>
    _animate.Visible ? _animate.Title
    : _color.Picker.Visible ? _color.Title
    : _fonts.Visible ? _fonts.Title
    : _grow.Visible ? _grow.Button.Label
    : null;

  public void Tick()
  {
    if (_frame == null)
      return;
    if (_frame.ViewChanged)
    {
      CloseOpen();
      Build();
      Render();
    }
    _frame.Tick();
    _dockPanel.Tick();
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
    bool searching = DockTyping();
    if (!searching)
      _keyboard.HandleKeys();
    if (_frame == null)
      return;
    _pointer.UpdateHover(Input.mousePosition);
    if (!searching)
      _keyboard.FollowComposition();
    bool blink = _reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f;
    // The text's caret rests while a formula or the value search has the input.
    _marks.Blink(!searching && !_text.HasSelection && !_formulas.Editing.Active && blink);
    if (!searching)
      _keyboard.UpdateImePosition();
    FollowLiveValues();
    if (_space.Moving || _zoom.Moving)
      RenderKeepingBlink();
  }

  // A field (the dock's value search, mostly) has the keys and the input
  // method while it is focused; the text takes both back once it lets go.
  private bool DockTyping()
  {
    bool typing = Keyboard.IsTaken;
    if (_dockTyping && !typing)
      Input.imeCompositionMode = IMECompositionMode.On;
    _dockTyping = typing;
    return typing;
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

  // A level playing behind, or the text gliding aside: drawn again, the carets keep blinking as they were.
  private void RenderKeepingBlink() => Draw(restartBlink: false);

  private OverlayValueMask LiveMask()
  {
    FormulaEditing editing = _formulas.Editing;
    OverlayValueMask mask = editing.Active ? editing.Working.Mask : OverlayValueMask.None;
    OverlayTextDocument document = _text.Document;
    for (int i = 0; i < document.Count; i++)
    {
      OverlayTextItem item = document[i];
      if (item.IsValue)
        mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.Value));
      mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.SizeFormula));
      mask |= OverlayTextComposer.Mask(OverlayFormulaText.Compile(item.Color.AlphaFormula));
      mask |= OverlayTextComposer.Mask(OverlayTextComposer.Cases(item.Color));
    }
    return mask | _preview.GradientMask;
  }

  // Rebuilds the text at its game size and everything placed around it, after
  // the player did something: the carets show at once and blink from there.
  private void Render() => Draw(restartBlink: true);

  private void Draw(bool restartBlink)
  {
    if (_frame == null)
      return;
    if (restartBlink)
    {
      _blinkStart = Time.unscaledTime;
    }
    FollowSelection();
    _formulas.Sync();
    // The picker edits a case's color while the formula ends in one.
    _color.Follow();
    // The text stays above the dock.
    _bottomTop = _dockPanel.Top;
    // The text keeps to the space left of the property column, or of an open
    // picker; it glides aside as the picker opens, and back as it closes.
    _space.Measure(_frame, _color.Picker, _dockPanel, _inspector, _reduceMotion(), out float left, out float right);
    _preview.Draw(
      left,
      right,
      EditorFramePanel.TopBar + 32,
      _bottomTop - 40,
      _frame.Scale,
      _elementScale,
      _zoom,
      _color.Picker.Visible,
      _reduceMotion()
    );
    _zoom.Say(_preview.Shown, (left + right) * .5f, EditorFramePanel.TopBar + 8);
    _map.Map();
    _marks.PlaceCaret();
    _marks.Place(_drops.InsertIndex, _drops.Joining, _formulas.EditedValue);
    ShowFields();
    _color.ShowSwatch();
    _color.Place(_dockPanel, _inspector);
    _gradient.Show();
    _fonts.Place(_dockPanel, _inspector.ToolTop);
    _inspector.Show(OpenTool(), OpenToolRoot());
    _formulas.Refresh(_dock);
    PlaceGrowMark();
    _marks.PlaceEmptyHint(_preview.Center, _preview.Empty, _frame.Scale);
    _liveMask = LiveMask();
  }

  // The column's rows show the range's font, size, color, animation and grow point.
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
    Rect text = _frame.FrameRect(_preview.Text.rectTransform);
    _grow.PlaceMark(text.x, text.y, text.width, text.height);
  }

  // The column's open tool goes on with a new selection, or with the whole
  // text once nothing is selected, however the selection changed (a click,
  // a drag, the keys, undo). First, before anything is drawn from it.
  private void FollowSelection()
  {
    _text.Range(out int start, out int end);
    if (!_text.HasSelection)
      start = end = -1;
    if (start == _toolStart && end == _toolEnd)
      return;
    _toolStart = start;
    _toolEnd = end;
    _color.Retarget();
    _fonts.Retarget();
  }

  private bool AnyOpen => _formulas.Open || _popovers.AnyOpen;

  private void CloseOpen()
  {
    CloseTyping();
    _popovers.CloseAll();
  }

  // The number or value being typed closes; the column's tool (the picker,
  // the font list, the grow points, Animate) stays.
  private void CloseTyping() => _formulas.Close();

  // The dimmed space around the text belongs to it; the header, the
  // property column, the popovers and the picker don't.
  private bool InTextArea(Vector2 screen)
  {
    if (
      _dockPanel.Contains(screen)
      || _popovers.Contains(screen)
      || _inspector.Contains(screen)
      || _animate.Contains(screen)
      || PopoverPlacement.Contains(_color.Picker.Root, screen)
    )
      return false;
    Vector2 at = _frame.ToFrame(screen);
    return at.y >= EditorFramePanel.TopBar && at.y < _bottomTop - 18 && at.x < _inspector.Left - 8;
  }

  // Undo or redo put an earlier text back; the keypad reads the restored formula afresh.
  private void Restored()
  {
    _formulas.Editing.Deactivate();
    Render();
  }

  /// <summary>
  /// Esc closes the picker first, then the Animate tool, then finishes like
  /// Done; only Cancel leaves without saving.
  /// </summary>
  public void Escape()
  {
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    else if (_animate.Visible)
    {
      _animate.Hide();
      Render();
    }
    else
      Close(true, true);
  }

  /// <summary>Done writes the runs to the element; cancel leaves it as it was.</summary>
  public void Close(bool commit, bool notify)
  {
    if (_frame == null)
      return;
    if (commit && _color.Picker.Visible && !_color.Picker.Commit())
      return;
    CloseOpen();
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
    _frame.Dispose();
    _frame = null;
    _drops.Release();
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
