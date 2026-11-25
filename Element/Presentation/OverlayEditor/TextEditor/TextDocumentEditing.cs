using System;
using System.Collections.Generic;
using System.Text;
using Element.Configuration.Overlay;
using Element.Domain.Judgement;
using Element.Domain.Overlay.Defaults;
using Element.Domain.Overlay.Text;
using Element.Domain.Overlay.Values;
using Element.Presentation.Overlay.Text;
using Element.Presentation.Overlay.Values;
using Element.Presentation.OverlayEditor.Parts;
using UnityEngine;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// The text being edited: its runs, the caret and the selection, the style
/// the next typed characters take, typing, deleting, the clipboard and undo.
/// Text copied here pastes back with its values, colors and sizes.
/// </summary>
internal sealed class TextDocumentEditing
{
  private readonly OverlayValues _values;
  private readonly Action _changed;
  private readonly Action _restored;
  private readonly StringBuilder _builder = new(64);
  private readonly EditorHistory<TextEditSnapshot> _history;
  private OverlayTextDocument _document = new();
  private OverlayTextDocument _opening = new();
  private List<OverlayTextItem> _clipboardItems;
  private string _clipboardText;
  private int _caretIndex;
  private int _anchor;
  private float _lastTypingAt = -1;

  /// <param name="changed">Redraws the editor after an edit or a caret move.</param>
  /// <param name="restored">Undo or redo put an earlier text back; the editor reads it afresh.</param>
  public TextDocumentEditing(OverlayValues values, Action changed, Action restored)
  {
    _values = values;
    _changed = changed;
    _restored = restored;
    _history = new EditorHistory<TextEditSnapshot>(Snapshot, Restore);
  }

  public OverlayTextDocument Document => _document;

  public int Caret
  {
    get => _caretIndex;
    set => _caretIndex = value;
  }

  /// <summary>Where the selection started; the caret is its other end.</summary>
  public int Anchor
  {
    get => _anchor;
    set => _anchor = value;
  }

  /// <summary>The x the caret keeps to across lines; NaN until it moves up or down.</summary>
  public float GoalX { get; set; } = float.NaN;

  /// <summary>Text an input method is composing at the caret.</summary>
  public string Composition { get; set; } = "";

  public bool HasSelection => _anchor != _caretIndex;

  /// <summary>Whether the text differs from what it was when opened.</summary>
  public bool Edited => !_document.SameAs(_opening);

  /// <summary>The style typing takes in an empty text, set before anything is typed.</summary>
  public OverlayColorSettings PendingColor { get; set; }

  public float PendingSize { get; set; }

  public string PendingSizeFormula { get; set; } = "";

  public string PendingFont { get; set; } = "";

  public void Open(OverlayTextSettings text)
  {
    _document = OverlayTextDocument.FromRuns(text?.Runs);
    _opening = _document.Clone();
    // Labels from converted texts become words in the game's language.
    _document.ReplaceValues(LegacyLabel);
    _caretIndex = _anchor = _document.Count;
    PendingColor = null;
    PendingSize = 0;
    PendingSizeFormula = "";
    PendingFont = "";
    _history.Clear();
  }

  private string LegacyLabel(string key)
  {
    if (!OverlayValueCatalog.TryGet(key, out OverlayValueDefinition value) || !OverlayValueCatalog.IsLegacy(value))
      return null;
    _builder.Clear();
    OverlayTextComposer.AppendValue(_builder, value.Id, _values, true);
    return _builder.ToString();
  }

  // ---- The selection and the style typing takes ----

  /// <summary>The selection, or the whole text when nothing is selected.</summary>
  public void Range(out int start, out int end)
  {
    start = HasSelection ? Mathf.Min(_anchor, _caretIndex) : 0;
    end = HasSelection ? Mathf.Max(_anchor, _caretIndex) : _document.Count;
  }

  public OverlayColorSettings RangeColor()
  {
    Range(out int start, out int end);
    return start < end ? _document[start].Color : TypingColor(_caretIndex);
  }

  /// <summary>The range's color, or the color typing will use.</summary>
  public void SetRangeColor(OverlayColorSettings color)
  {
    Range(out int start, out int end);
    SetColor(start, end, color);
  }

  /// <summary>Colors the items from <paramref name="start"/> to <paramref name="end"/>; with none, typing takes it.</summary>
  public void SetColor(int start, int end, OverlayColorSettings color)
  {
    if (start < end)
      _document.SetColor(start, end, color);
    else
      PendingColor = color;
  }

  /// <summary>The range's font: empty follows the default, null mixes several.</summary>
  public string RangeFont()
  {
    Range(out int start, out int end);
    return start < end ? _document.RangeFont(start, end) : TypingFont(_caretIndex);
  }

  public OverlayColorSettings TypingColor(int caret) =>
    _document.Count == 0 && PendingColor != null ? PendingColor : _document.TypingColor(caret);

  public float TypingSize(int caret) =>
    _document.Count == 0 && PendingSize > 0 ? PendingSize : _document.TypingSize(caret);

  public string TypingSizeFormula(int caret) =>
    _document.Count == 0 ? PendingSizeFormula : _document.TypingSizeFormula(caret);

  public string TypingFont(int caret) => _document.Count == 0 ? PendingFont : _document.TypingFont(caret);

  /// <summary>Whether exactly one value is selected, and which.</summary>
  public bool SelectedValue(out int index)
  {
    index = Mathf.Min(_anchor, _caretIndex);
    return Mathf.Abs(_anchor - _caretIndex) == 1 && index < _document.Count && _document[index].IsValue;
  }

  // ---- Typing and moving ----

  public void Type(string text)
  {
    bool coalesce = !HasSelection && Time.unscaledTime - _lastTypingAt < 1f && text != "\n";
    Remember(coalesce);
    _lastTypingAt = Time.unscaledTime;
    int start = Mathf.Min(_anchor, _caretIndex);
    _caretIndex = _anchor = _document.InsertText(
      start,
      Mathf.Max(_anchor, _caretIndex),
      text,
      TypingColor(start),
      TypingSize(start),
      TypingSizeFormula(start),
      TypingFont(start)
    );
    Changed();
  }

  public void DeleteSelection()
  {
    DeleteRange(Mathf.Min(_anchor, _caretIndex), Mathf.Max(_anchor, _caretIndex));
  }

  public void DeleteRange(int start, int end)
  {
    if (start == end)
      return;
    Remember(false);
    _document.Delete(start, end);
    _caretIndex = _anchor = Mathf.Min(start, end);
    Changed();
  }

  public void MoveTo(int index, bool extend, bool keepGoal = false)
  {
    _caretIndex = _document.Clamp(index);
    if (!extend)
      _anchor = _caretIndex;
    if (!keepGoal)
      GoalX = float.NaN;
    _changed();
  }

  public void SelectAll()
  {
    _anchor = 0;
    _caretIndex = _document.Count;
    _changed();
  }

  public bool Copy()
  {
    if (!HasSelection)
      return false;
    int start = Mathf.Min(_anchor, _caretIndex),
      end = Mathf.Max(_anchor, _caretIndex);
    _clipboardItems = _document.Slice(start, end);
    _clipboardText = _document.PlainText(start, end, key => "{" + key + "}");
    GUIUtility.systemCopyBuffer = _clipboardText;
    return true;
  }

  // Text copied here pastes back with its values, colors and sizes; anything else is plain text.
  public void Paste()
  {
    string system = GUIUtility.systemCopyBuffer;
    int start = Mathf.Min(_anchor, _caretIndex);
    if (_clipboardItems != null && system == _clipboardText)
    {
      Remember(false);
      _caretIndex = _anchor = _document.InsertItems(start, Mathf.Max(_anchor, _caretIndex), _clipboardItems);
      Changed();
    }
    else if (!string.IsNullOrEmpty(system))
    {
      Remember(false);
      _caretIndex = _anchor = _document.InsertText(
        start,
        Mathf.Max(_anchor, _caretIndex),
        system,
        TypingColor(start),
        TypingSize(start),
        TypingSizeFormula(start),
        TypingFont(start)
      );
      Changed();
    }
  }

  /// <summary>A new value replaces the range and stays selected.</summary>
  public void InsertValue(OverlayValueDefinition value, int start, int end) =>
    Insert(value.Key, InsertColor(value.Id), start, end);

  /// <summary>A value made on the spot (<paramref name="formula"/>) replaces the range, in the color typing has there.</summary>
  public void InsertFormula(string formula, int start, int end) =>
    Insert(formula, TypingColor(_document.Clamp(start)), start, end);

  private void Insert(string formula, OverlayColorSettings color, int start, int end)
  {
    Remember(false);
    start = _document.Clamp(start);
    int after = _document.InsertValue(
      start,
      end,
      formula,
      color,
      TypingSize(start),
      TypingSizeFormula(start),
      TypingFont(start),
      _document.TypingAnimate(start)
    );
    _anchor = start;
    _caretIndex = after;
    GoalX = float.NaN;
  }

  // Values keep their meaning's color when inserted: judgment counts their
  // judgment's color, plain counts white, the rest the highlight color.
  private static OverlayColorSettings InsertColor(OverlayValueId id) =>
    id switch
    {
      OverlayValueId.TooEarly or OverlayValueId.TooLate => OverlayColorSettings.Custom("#FF0000"),
      OverlayValueId.VeryEarly or OverlayValueId.VeryLate => OverlayColorSettings.Custom("#FF6F4E"),
      OverlayValueId.EarlyPerfect or OverlayValueId.LatePerfect => OverlayColorSettings.Custom("#A0FF4E"),
      OverlayValueId.PerfectEarly or OverlayValueId.PurePerfect or OverlayValueId.PerfectLate =>
        OverlayColorSettings.Custom(JudgementColors.Perfect),
      OverlayValueId.Overload or OverlayValueId.Miss => OverlayColorSettings.Custom(
        OverlayElementDefaults.JudgementBase
      ),
      OverlayValueId.SongTitle or OverlayValueId.Attempt or OverlayValueId.FullAttempt or OverlayValueId.TimingScale =>
        OverlayColorSettings.Custom("#FFFFFF"),
      _ => OverlayColorSettings.HighlightColor(),
    };

  private void Changed()
  {
    GoalX = float.NaN;
    _changed();
  }

  // ---- Undo ----

  /// <summary>Takes the text before a change; typing within a second joins the last step.</summary>
  public void Remember(bool coalesce)
  {
    _history.Remember(coalesce);
    if (!coalesce)
      _lastTypingAt = -1;
  }

  /// <summary>Keypad edits of one place within a second make one undo step.</summary>
  public void RememberFormula(string key) => Remember(_history.Continues(key));

  /// <summary>The next keypad edit starts its own undo step.</summary>
  public void BreakFormula() => _history.Break();

  public void Undo() => _history.Undo();

  public void Redo() => _history.Redo();

  private TextEditSnapshot Snapshot() => new(_document.Clone(), _caretIndex, _anchor);

  private void Restore(TextEditSnapshot snapshot)
  {
    _document = snapshot.Document.Clone();
    _caretIndex = snapshot.Caret;
    _anchor = snapshot.Anchor;
    _lastTypingAt = -1;
    _history.Break();
    _restored();
  }
}
