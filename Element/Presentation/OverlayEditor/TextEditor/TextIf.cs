using System;
using System.Collections.Generic;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Overlay.Conditions;
using Element.Domain.Overlay.Formula;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using Element.Presentation.OverlayEditor.Parts.EditorFrame;
using Element.Presentation.OverlayEditor.Parts.Formula;
using Element.Presentation.OverlayEditor.Parts.IfTool;
using Element.Presentation.OverlayEditor.Parts.Inspector;
using Element.Presentation.OverlayEditor.Parts.Property;
using Element.Presentation.Ui.ColorPicker;
using Element.Presentation.Ui.Declarative;
using Element.Presentation.Ui.Graphics;
using Element.Presentation.Ui.Popovers;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The column's If row and the tool it opens there. An If makes a stretch of
/// the text (the selection, or the whole text with nothing selected) look
/// different from case to case: each case compares a value with a number and
/// has a look of its own, and "otherwise" has one for when no case is on.
/// </summary>
/// <remarks>
/// A look is not set here: picking a card puts its look in the preview with
/// the caret in it, and from then on typing and the column's other tools
/// (color, font, size) change that look alone
/// (<see cref="TextDocumentEditing.Range"/>). The tool acts on the If the
/// selection touches, or the caret is in, or else the text's first. Trying a value
/// moves the compared value by hand, so every case can be seen without
/// playing a level.
/// </remarks>
internal sealed class TextIf : IIfToolActions
{
  /// <summary>What the dock's number keys start with for a case's number, followed by the case.</summary>
  public const string KeyPrefix = "if:";

  private const float CaptionWidth = 420,
    CaptionHeight = 20;

  private readonly ITextEditorActions _editor;
  private readonly ITextEditorContext _context;
  private readonly TextDocumentEditing _text;
  private readonly TextFormulas _formulas;
  private readonly TextColor _color;
  private readonly TextPreview _preview;
  private readonly OverlayValues _values;
  private readonly TextValueSource _source;
  private readonly StringBuilder _builder = new(96);
  private readonly List<OverlayValueId> _found = new();
  private PropertyButtonViewModel _ifButton;
  private IfToolViewModel _model;
  private ScreenPanel _panel;
  private InspectorPanel _inspector;
  private TextMeshProUGUI _caption;
  private string _captionSaid;

  // The case whose value is being picked from the menu or the dock; -1 for none.
  private int _picking = -1;

  // The value the slider tries, and the numbers at its two ends.
  private OverlayValueDefinition _tried;
  private double _tryLow,
    _tryHigh;

  public TextIf(
    ITextEditorActions editor,
    ITextEditorContext context,
    TextDocumentEditing text,
    TextFormulas formulas,
    TextColor color,
    TextPreview preview,
    OverlayValues values
  )
  {
    _editor = editor;
    _context = context;
    _text = text;
    _formulas = formulas;
    _color = color;
    _preview = preview;
    _values = values;
    _source = new TextValueSource(editor, text, values, Picked);
  }

  /// <summary>The column's If row; made by <see cref="BuildButton"/>.</summary>
  public PropertyButtonViewModel Button => _ifButton;

  /// <summary>What the tool shows; made by <see cref="Build"/>.</summary>
  public IfToolViewModel ViewModel => _model;

  /// <summary>The tool is open in the column.</summary>
  public bool Visible => _panel != null && _panel.Root.gameObject.activeSelf;

  public RectTransform Root => _panel?.Root;

  /// <summary>The menu of values to compare.</summary>
  public IPopover Menu => _source.Menu;

  /// <summary>Waiting for a value to compare: the next value clicked in the dock is it.</summary>
  public bool Armed => _source.Armed;

  /// <summary>What the column's back row names while the tool is open.</summary>
  public string Title => _text.HasSelection ? L("선택한 글자의 만약", "If for the selection") : L("만약", "If");

  public PropertyButtonViewModel BuildButton() =>
    _ifButton = new PropertyButtonViewModel(
      "Text if",
      L("만약", "If"),
      Open,
      value: true,
      icon: UiIconKind.If,
      opens: true
    );

  /// <summary>The tool, hidden until the row opens it, under <paramref name="inspector"/>'s back row.</summary>
  public void Build(EditorFramePanel screen, InspectorPanel inspector)
  {
    _inspector = inspector;
    _model = new IfToolViewModel(this, UiPalette.EditorDim);
    float room = Mathf.Max(120, inspector.Bottom - inspector.ToolTop);
    _panel = screen.Panel("If tool", Color.clear);
    _panel.Mount(
      screen.NewBackend(),
      new IfToolView(_model, inspector.Width, room),
      _model,
      inspector.Width,
      followsContent: true
    );
    _panel.Root.gameObject.SetActive(false);
    _source.Build(screen, ValueRect);
    _caption = screen.Ui.Text("", screen.Frame, 0, 0, CaptionWidth, CaptionHeight, 12, UiPalette.SoftAccent);
    _caption.name = "If look";
    _caption.alignment = TextAlignmentOptions.Midline;
    _caption.textWrappingMode = TextWrappingModes.NoWrap;
    _caption.overflowMode = TextOverflowModes.Ellipsis;
    _caption.raycastTarget = false;
    _caption.gameObject.SetActive(false);
    _captionSaid = null;
    _picking = -1;
    _tried = null;
  }

  /// <summary>Whether a screen point (origin bottom left) is over the open tool.</summary>
  public bool Contains(Vector2 screen) => Visible && PopoverPlacement.Contains(_panel.Root, screen);

  /// <summary>The back row: the tool closes, and with it the value menu and the tried value.</summary>
  public void Hide()
  {
    if (!Visible)
      return;
    _panel.Root.gameObject.SetActive(false);
    Closed();
  }

  /// <summary>The editor closes, or the tool does: the tried value reads as the game has it again.</summary>
  public void Closed()
  {
    ClosePicking();
    _values.StopTrying();
  }

  /// <summary>The menu of values closes and the dock goes back to adding values at the caret.</summary>
  public void ClosePicking() => _source.Close();

  /// <summary>The selection changed: a number being typed for another If's case is left.</summary>
  public void Retarget()
  {
    if (_formulas.Field != null && Owns(_formulas.Field))
      _editor.CloseTyping();
  }

  /// <summary>A value clicked in the dock while <see cref="Armed"/> is the one to compare.</summary>
  public void PickFromDock(OverlayValueDefinition value) => _source.PickFromDock(value);

  // The row opens the tool in the column; the back row closes it.
  private void Open()
  {
    _editor.CloseOpen();
    UiColorPicker picker = _color.Picker;
    if (picker.Visible)
      picker.Hide();
    _panel.Root.gameObject.SetActive(true);
    _panel.Root.SetAsLastSibling();
    _editor.Render();
  }

  // ---- Which If, and what the dock types ----

  // The If the tool acts on: the first the selection touches; with nothing
  // selected the one the caret is in, or else the text's first.
  private int Target()
  {
    _text.Range(out int start, out int end);
    return _text.Document.BlockIn(start, end);
  }

  /// <summary>Whether <paramref name="key"/> is a case's number.</summary>
  public static bool Owns(string key) => key != null && key.StartsWith(KeyPrefix, StringComparison.Ordinal);

  /// <summary>How the keypad reads and writes the number <paramref name="key"/> names; null when it names none here.</summary>
  public FormulaSlot Slot(string key)
  {
    if (!Owns(key) || !int.TryParse(key.Substring(KeyPrefix.Length), out int index))
      return null;
    int block = Target();
    if (block < 0 || index < 0 || index >= _text.Document.Block(block).Cases)
      return null;
    return new FormulaSlot(
      key,
      false,
      () => OverlayFormula.Parse(_text.Document.Block(block).When(index).Than),
      formula =>
      {
        _text.RememberFormula(key);
        OverlayCondition when = _text.Document.Block(block).When(index).Clone();
        when.Than = formula.Serialize();
        _text.Document.SetCase(block, index, when);
      }
    );
  }

  /// <summary>What the dock calls the number <paramref name="key"/> names; null when it is none of these.</summary>
  public static string Name(string key) => Owns(key) ? L("비교할 숫자", "Number to compare") : null;

  /// <summary>The values every condition in the text reads, so a level playing behind moves the cards.</summary>
  public OverlayValueMask Mask
  {
    get
    {
      OverlayValueMask mask = OverlayValueMask.None;
      OverlayTextDocument document = _text.Document;
      for (int block = 0; block < document.BlockCount; block++)
      {
        OverlayTextBlock target = document.Block(block);
        for (int i = 0; i < target.Cases; i++)
          mask |= OverlayFormula.Parse(target.When(i).Value).Mask | OverlayFormula.Parse(target.When(i).Than).Mask;
      }
      return mask;
    }
  }

  // ---- What the tool asks for ----

  void IIfToolActions.AddCase()
  {
    _editor.CloseOpen();
    OverlayTextDocument document = _text.Document;
    _text.Remember(false);
    int block = Target();
    if (block < 0)
    {
      _text.Range(out int start, out int end);
      block = document.AddBlock(start, end);
    }
    int index = block < 0 ? -1 : document.AddCase(block, NewCase(block));
    if (index < 0)
    {
      _editor.Notice(
        L(
          "경우는 " + OverlayTextIf.MaximumCases + "개까지 만들 수 있어요.",
          "An If holds up to " + OverlayTextIf.MaximumCases + " cases."
        )
      );
      _editor.Render();
      return;
    }
    document.Show(block, index);
    SelectLook(block);
    _editor.Notice(
      L(
        "지금 보이는 게 이 경우의 모습이에요. 글자를 고치거나 색을 바꿔 보세요.",
        "This is how the case looks. Change its text or color."
      ),
      false
    );
    _editor.Render();
    // Nothing to compare yet: the values to pick from open at once.
    if (string.IsNullOrEmpty(document.Block(block).When(index).Value))
      ((IIfToolActions)this).PickValue(index);
  }

  void IIfToolActions.RemoveCase(int index)
  {
    int block = Target();
    if (block < 0)
      return;
    _editor.CloseOpen();
    _text.Remember(false);
    _text.Document.RemoveCase(block, index);
    int left = Target();
    if (left >= 0)
      SelectLook(left);
    else
      _text.Caret = _text.Anchor = _text.Document.Clamp(_text.Caret);
    _editor.Render();
  }

  void IIfToolActions.PickValue(int index)
  {
    int block = Target();
    if (block < 0)
      return;
    // Which case the value is for stays known while the menu or the dock is picked from.
    _picking = index;
    _source.Toggle(Key(OverlayFormula.Parse(_text.Document.Block(block).When(index).Value)));
  }

  void IIfToolActions.EditNumber(int index) => _formulas.Activate(KeyPrefix + index);

  void IIfToolActions.Toggle(int index, OverlayComparison result)
  {
    int block = Target();
    if (block < 0)
      return;
    _editor.CloseOpen();
    _text.Remember(false);
    OverlayCondition when = _text.Document.Block(block).When(index).Clone();
    if (result == OverlayComparison.Smaller)
      when.Smaller = !when.Smaller;
    else if (result == OverlayComparison.Same)
      when.Same = !when.Same;
    else
      when.Bigger = !when.Bigger;
    _text.Document.SetCase(block, index, when);
    _editor.Render();
  }

  void IIfToolActions.ShowLook(int look)
  {
    int block = Target();
    if (block < 0)
      return;
    _editor.CloseOpen();
    _text.Document.Show(block, look);
    SelectLook(block);
    _editor.Render();
  }

  void IIfToolActions.Try(float position)
  {
    int block = Target();
    if (block < 0 || _tried == null)
      return;
    double number = Snapped(block, _tryLow + (_tryHigh - _tryLow) * Mathf.Clamp01(position));
    _values.Try(_tried.Id, number);
    // The look for that number shows, as it would in the game.
    OverlayTextBlock target = _text.Document.Block(block);
    int on = On(target);
    if (on != target.Shown)
    {
      _editor.CloseOpen();
      _text.Document.Show(block, on);
      SelectLook(block);
    }
    _editor.Render();
  }

  // A value picked from the menu or the dock is what the case compares.
  private void Picked(OverlayValueDefinition value)
  {
    int block = Target();
    int index = _picking;
    _picking = -1;
    if (block < 0 || index < 0 || index >= _text.Document.Block(block).Cases)
    {
      _editor.Render();
      return;
    }
    _text.Remember(false);
    OverlayCondition when = _text.Document.Block(block).When(index).Clone();
    when.Value = OverlayFormula.FromValue(value.Id).Serialize();
    if (string.IsNullOrEmpty(when.Than))
      when.Than = StartingNumber(value);
    _text.Document.SetCase(block, index, when);
    _editor.Render();
  }

  // ---- Making a case ----

  // A new case compares what the last one does, so tiers of one value take
  // one press each. The first compares the first number its stretch shows,
  // or else the text's, with the round number at or above where it is now.
  private OverlayCondition NewCase(int block)
  {
    OverlayTextDocument document = _text.Document;
    OverlayTextBlock target = document.Block(block);
    if (target.Cases > 0)
      return target.When(target.Cases - 1).Clone();
    OverlayCondition when = new() { Same = true, Bigger = true };
    OverlayValueDefinition value = FirstNumber(target.Start, target.End) ?? FirstNumber(0, document.Count);
    if (value == null)
      return when;
    when.Value = OverlayFormula.FromValue(value.Id).Serialize();
    when.Than = StartingNumber(value);
    return when;
  }

  private OverlayValueDefinition FirstNumber(int start, int end)
  {
    _found.Clear();
    _text.Document.ValuesIn(start, end, _found);
    foreach (OverlayValueId id in _found)
      if (OverlayValueCatalog.Get(id).IsNumeric)
        return OverlayValueCatalog.Get(id);
    return null;
  }

  private string StartingNumber(OverlayValueDefinition value)
  {
    double now = _source.Number(value, out _);
    return OverlayFormula.NumberText(double.IsNaN(now) ? 0 : Math.Ceiling(now));
  }

  // The caret goes to the end of the look shown, with nothing selected: the
  // column's tools then act on that look alone, and it shows in its own colors.
  private void SelectLook(int block)
  {
    OverlayTextBlock target = _text.Document.Block(block);
    _text.Anchor = _text.Caret = target.End;
    _text.GoalX = float.NaN;
  }

  // ---- Showing ----

  /// <summary>
  /// The row says how many cases the If has; the open tool shows its cards
  /// and sits under the column's back row; a line over the preview names the
  /// look in it.
  /// </summary>
  public void Show()
  {
    OverlayTextDocument document = _text.Document;
    int block = Target();
    OverlayTextBlock target = block >= 0 ? document.Block(block) : null;
    _ifButton.Set(
      target == null
        ? L("없음", "None")
        : L("경우 " + target.Cases + "개", target.Cases == 1 ? "1 case" : target.Cases + " cases"),
      false,
      Visible
    );
    ShowCaption(target);
    if (!Visible)
      return;
    _model.ShowCases(target != null, target?.Cases ?? 0);
    if (target == null)
      _model.ShowTry(false, null, null, 0);
    else
    {
      int on = On(target);
      for (int i = 0; i < target.Cases; i++)
        ShowCase(block, target, i, on == i);
      LookLine(block, target.Otherwise, out string text, out Color ink, out bool hidden);
      _model.Otherwise.ShowLookLine(
        target.Otherwise,
        target.Shown == target.Otherwise,
        on == target.Otherwise,
        text,
        ink,
        hidden
      );
      ShowTry(target);
    }
    _panel.Sync();
    _panel.Root.anchoredPosition = new Vector2(Mathf.Round(_inspector.Left), -Mathf.Round(_inspector.ToolTop));
  }

  // The first case on now, as the values (or the editor's examples) are; otherwise with none.
  private int On(OverlayTextBlock target)
  {
    IOverlayNumberSource numbers = _values.WithSamples;
    for (int i = 0; i < target.Cases; i++)
      if (new OverlayConditionCheck(target.When(i)).Holds(numbers))
        return i;
    return target.Otherwise;
  }

  private void ShowCase(int block, OverlayTextBlock target, int index, bool now)
  {
    OverlayCondition when = target.When(index);
    IOverlayNumberSource numbers = _values.WithSamples;
    OverlayFormula value = OverlayFormula.Parse(when.Value);
    string name = value.IsEmpty ? L("값 고르기", "Pick a value") : value.Describe(ValueSectionNames.ValueName);
    string valueNow = value.TryEvaluate(numbers, out double number, out bool time) ? Number(number, time) : "";
    string key = KeyPrefix + index;
    bool typing = _formulas.Edits(key, out OverlayFormula typed);
    OverlayFormula than = typed ?? OverlayFormula.Parse(when.Than);
    string thanText = than.IsEmpty ? "—" : IfWords.Number(_formulas.Result(than));
    IfCaseViewModel card = _model.Card(index);
    card.Show(
      name,
      valueNow,
      value.IsEmpty,
      _picking == index && (_source.Open || _source.Armed),
      thanText,
      !than.IsEmpty && !than.IsPlainNumber(out _),
      typing,
      when.Smaller,
      when.Same,
      when.Bigger,
      new OverlayConditionCheck(when).Compare(numbers),
      name + " · " + IfWords.When(thanText, when.Smaller, when.Same, when.Bigger)
    );
    LookLine(block, index, out string text, out Color ink, out bool hidden);
    card.ShowLookLine(index, target.Shown == index, now, text, ink, hidden);
  }

  // A look as one line: its characters and what its values show now, in its first color.
  private void LookLine(int block, int look, out string text, out Color ink, out bool hidden)
  {
    OverlayTextDocument document = _text.Document;
    int count = document.LookCount(block, look);
    _builder.Clear();
    ink = Color.white;
    for (int i = 0; i < count; i++)
    {
      OverlayTextItem item = document.LookItem(block, look, i);
      if (i == 0)
        ink = OverlayTextComposer.Resolve(item.Color, UiTheme.Accent);
      if (item.IsValue)
        OverlayFormulaText.Append(_builder, OverlayFormula.Parse(item.Value), _values, true);
      else
        _builder.Append(item.Text == "\n" ? " " : item.Text);
    }
    hidden = count == 0;
    text = hidden ? L("아무것도 안 보여요", "Shows nothing") : _builder.ToString();
  }

  // The slider tries the first case's value, when it compares one value
  // alone, from a little under the cases' lowest number to a little over
  // their highest.
  private void ShowTry(OverlayTextBlock target)
  {
    _tried = null;
    OverlayFormula value = OverlayFormula.Parse(target.When(0).Value);
    if (!value.IsSingleValue(out OverlayValueId id) || !OverlayValueCatalog.Get(id).IsNumeric)
    {
      _model.ShowTry(false, null, null, 0);
      return;
    }
    double low = double.PositiveInfinity,
      high = double.NegativeInfinity;
    for (int i = 0; i < target.Cases; i++)
    {
      double than = _formulas.Result(OverlayFormula.Parse(target.When(i).Than));
      if (double.IsNaN(than) || double.IsInfinity(than))
        continue;
      low = Math.Min(low, than);
      high = Math.Max(high, than);
    }
    if (low > high)
    {
      _model.ShowTry(false, null, null, 0);
      return;
    }
    double room = Math.Max(Math.Max((high - low) * .5, Math.Abs(high) * .1), 2);
    _tried = OverlayValueCatalog.Get(id);
    // Counts and percents don't go below nothing.
    _tryLow = low >= 0 ? Math.Max(0, low - room) : low - room;
    _tryHigh = high + room;
    bool has = _values.WithSamples.TryGetNumber(id, out double now);
    _model.ShowTry(
      true,
      ValueSectionNames.ValueName(_tried),
      has ? Number(now, _tried.Kind == OverlayValueKind.Time) : "",
      has ? (float)((now - _tryLow) / (_tryHigh - _tryLow)) : 0
    );
  }

  // A tried number lands on a case's own number when close to it, so "the
  // same" can be reached; otherwise on a tidy step of the range.
  private double Snapped(int block, double number)
  {
    OverlayTextBlock target = _text.Document.Block(block);
    double span = _tryHigh - _tryLow;
    for (int i = 0; i < target.Cases; i++)
    {
      double than = _formulas.Result(OverlayFormula.Parse(target.When(i).Than));
      if (!double.IsNaN(than) && Math.Abs(than - number) <= span * .02)
        return than;
    }
    double step = Math.Pow(10, Math.Floor(Math.Log10(span / 100)));
    return Math.Round(number / step) * step;
  }

  // Over the preview: which look is in it, while the text has an If.
  private void ShowCaption(OverlayTextBlock target)
  {
    bool say = target != null;
    if (_caption.gameObject.activeSelf != say)
      _caption.gameObject.SetActive(say);
    if (!say)
      return;
    string said;
    if (target.Shown == target.Otherwise)
      said = L("만약 · 아니면", "If · otherwise");
    else
    {
      OverlayCondition when = target.When(target.Shown);
      OverlayFormula value = OverlayFormula.Parse(when.Value);
      OverlayFormula than = OverlayFormula.Parse(when.Than);
      string name = value.IsEmpty ? L("값", "a value") : value.Describe(ValueSectionNames.ValueName);
      string number = than.IsEmpty ? "—" : IfWords.Number(_formulas.Result(than));
      said = L(
        "만약 · " + name + IfWords.Subject(name) + " " + IfWords.When(number, when.Smaller, when.Same, when.Bigger),
        "If · " + name + " is " + IfWords.When(number, when.Smaller, when.Same, when.Bigger)
      );
    }
    if (said != _captionSaid)
      _caption.text = _captionSaid = said;
    float scale = _context.Frame.Scale;
    UiFactory.Place(
      _caption.rectTransform,
      _preview.Center.x / scale - CaptionWidth * .5f,
      Mathf.Max(EditorFramePanel.TopBar + 30, _preview.Top - CaptionHeight - 10),
      CaptionWidth,
      CaptionHeight
    );
  }

  /// <summary>
  /// What the dock's line says while the tool is open: what an If is before
  /// there is one, then which look is in the preview and how to change it.
  /// Null while the tool is closed.
  /// </summary>
  public string Hint()
  {
    if (!Visible)
      return null;
    if (_source.Armed)
      return L("아래 값 목록에서 비교할 값을 눌러 주세요", "Click the value to compare in the values below");
    int block = Target();
    if (block < 0)
      return _text.HasSelection
        ? L(
          "고른 글자가 값에 따라 다른 모습으로 바뀌어요 · 만약 만들기를 눌러요",
          "The selection looks different as a value changes · Press Make an If"
        )
        : L(
          "이 글이 값에 따라 다른 모습으로 바뀌어요 · 만약 만들기를 눌러요",
          "This text looks different as a value changes · Press Make an If"
        );
    return L(
      "카드를 누르면 그 경우의 모습이 보여요 · 글자를 고치거나 색·폰트·크기를 바꾸면 그 모습만 바뀌어요",
      "Press a card to see its look · Typing, color, font and size change that look alone"
    );
  }

  // Where the value chip of the case being picked for is, in frame units, for the menu under it.
  private Rect ValueRect()
  {
    Selectable chip = _picking >= 0 ? _panel.Control<Selectable>("If value " + _picking) : null;
    return chip != null && chip.gameObject.activeInHierarchy
      ? _context.Frame.FrameRect((RectTransform)chip.transform)
      : new Rect(_inspector.Left, _inspector.ToolTop, _inspector.Width, 0);
  }

  private string Number(double number, bool time)
  {
    if (!time)
      return IfWords.Number(number);
    _builder.Clear();
    OverlayFormulaText.AppendResult(_builder, number, true);
    return _builder.ToString();
  }

  // The key of the one value a formula is, as the menu ticks it; empty for anything else.
  private static string Key(OverlayFormula formula) =>
    formula.IsSingleValue(out OverlayValueId id) ? OverlayValueCatalog.Get(id).Key : "";
}
