using System.Collections.Generic;
using System.Globalization;
using Element.Configuration;
using Element.Domain.Elements;
using Element.Presentation.Overlay.Elements;
using Element.Presentation.Overlay.Shared;
using Element.Presentation.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Settings.SettingsLocale;

namespace Element.Presentation.Layout;

// Caret, selection, typing (including IME composition), mouse, clipboard and undo.
internal sealed partial class OverlayTextEditor
{
  private readonly List<int> _start = new();
  private readonly List<int> _length = new();
  private readonly List<Snapshot> _undo = new();
  private readonly List<Snapshot> _redo = new();
  private readonly Event _event = new();
  private OverlayTextDocument _document = new();
  private OverlayTextDocument _opening = new();
  private List<OverlayTextItem> _clipboardItems;
  private string _clipboardText;
  private OverlayColorSettings _pendingColor;
  private float _pendingSize;
  private int _caretIndex;
  private int _anchor;
  private float _goalX = float.NaN;
  private string _composition = "";
  private int _compositionStart = -1;
  private bool _dragging;
  private float _lastClick = -1;
  private float _blinkStart;
  private float _lastTypingAt = -1;
  private bool _pickerSnapshotTaken;
  private float _previewScale = 1;

  private bool HasSelection => _anchor != _caretIndex;

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
    if (_picker.Visible && _picker.IsTyping)
    {
      if (Input.GetKeyDown(KeyCode.Escape))
        _picker.Hide();
      return;
    }
    HandleMouse();
    HandleKeys();
    if (_screen == null)
      return;
    UpdateHover(Input.mousePosition);
    // A formula takes keys, not IME text.
    string composition = _editing.Active ? "" : Input.compositionString ?? "";
    if (composition != _composition)
    {
      if (composition.Length > 0 && HasSelection)
        DeleteSelection();
      _composition = composition;
      Render();
    }
    bool blink = _reduceMotion() || (Time.unscaledTime - _blinkStart) % 1.06f < .53f;
    // The text's caret rests while a formula has the input.
    _caret.enabled = !HasSelection && !_editing.Active && blink;
    _emptyCaret.enabled = _caret.enabled;
    _dock.Blink(blink);
    UpdateImePosition();
  }

  private OverlayColorSettings TypingColor(int caret) =>
    _document.Count == 0 && _pendingColor != null ? _pendingColor : _document.TypingColor(caret);

  private float TypingSize(int caret) =>
    _document.Count == 0 && _pendingSize > 0 ? _pendingSize : _document.TypingSize(caret);

  private string TypingSizeFormula(int caret) =>
    _document.Count == 0 ? _pendingSizeFormula : _document.TypingSizeFormula(caret);

  // While a popover is open: Enter and Esc close it, and with a formula open
  // the keyboard types into it like the keypad. Other keys go back to the
  // text; after a value, typing continues behind it.
  private bool HandlePopoverKey(bool command)
  {
    if (command || !AnyPopover)
      return false;
    KeyCode code = _event.keyCode;
    if (code == KeyCode.Escape || code == KeyCode.Return || code == KeyCode.KeypadEnter)
    {
      ClosePopovers();
      Render();
      return true;
    }
    if (_editing.Active && OverlayKeypad.TryKeyboard(_event, out char key))
    {
      KeypadPress(key);
      return true;
    }
    if (_fieldSlot != null)
      return true;
    bool chip = ChipOpen(out _);
    ClosePopovers();
    if (chip)
      _anchor = _caretIndex = Mathf.Max(_anchor, _caretIndex);
    Render();
    return false;
  }

  private void HandleKeys()
  {
    bool mac =
      UnityEngine.Application.platform == RuntimePlatform.OSXPlayer
      || UnityEngine.Application.platform == RuntimePlatform.OSXEditor;
    while (_screen != null && Event.PopEvent(_event))
    {
      if (_event.rawType != EventType.KeyDown)
        continue;
      bool command = mac ? _event.command : _event.control;
      bool word = mac ? _event.alt : _event.control;
      bool shift = _event.shift;
      if (HandlePopoverKey(command))
        continue;
      switch (_event.keyCode)
      {
        case KeyCode.Escape:
          if (_picker.Visible)
            _picker.Hide();
          else
            Close(false, true);
          return;
        case KeyCode.Return:
        case KeyCode.KeypadEnter:
          if (command)
          {
            Close(true, true);
            return;
          }
          if (_composition.Length == 0)
            Type("\n");
          continue;
        case KeyCode.Backspace:
          if (_composition.Length > 0)
            continue;
          if (HasSelection)
            DeleteSelection();
          else if (_caretIndex > 0)
            DeleteRange(
              word ? _document.WordStart(_caretIndex)
                : command ? LineStart(_caretIndex)
                : _caretIndex - 1,
              _caretIndex
            );
          continue;
        case KeyCode.Delete:
          if (HasSelection)
            DeleteSelection();
          else if (_caretIndex < _document.Count)
            DeleteRange(_caretIndex, word ? _document.WordEnd(_caretIndex) : _caretIndex + 1);
          continue;
        case KeyCode.LeftArrow:
          MoveHorizontally(-1, shift, word, command);
          continue;
        case KeyCode.RightArrow:
          MoveHorizontally(1, shift, word, command);
          continue;
        case KeyCode.UpArrow:
          MoveTo(command ? 0 : LineStep(-1), shift, true);
          continue;
        case KeyCode.DownArrow:
          MoveTo(command ? _document.Count : LineStep(1), shift, true);
          continue;
        case KeyCode.Home:
          MoveTo(LineStart(_caretIndex), shift);
          continue;
        case KeyCode.End:
          MoveTo(LineEnd(_caretIndex), shift);
          continue;
      }
      if (command)
      {
        switch (_event.keyCode)
        {
          case KeyCode.A:
            _anchor = 0;
            _caretIndex = _document.Count;
            Render();
            break;
          case KeyCode.C:
            Copy();
            break;
          case KeyCode.X:
            if (Copy())
              DeleteSelection();
            break;
          case KeyCode.V:
            Paste();
            break;
          case KeyCode.Z:
            if (shift)
              Redo();
            else
              Undo();
            break;
          case KeyCode.Y:
            Redo();
            break;
        }
        continue;
      }
      char c = _event.character;
      if (c == '\r' || c == '\n' || c == '\t' || c == 0 || char.IsControl(c))
        continue;
      Type(c.ToString());
    }
  }

  private void HandleMouse()
  {
    Vector2 screen = Input.mousePosition;
    if (_dragValue != null || _dock.Dragging)
      return;
    if (Input.GetMouseButtonDown(0))
    {
      // Popovers, the bar and the picker handle their own clicks.
      if (
        _dock.Contains(screen)
        || _growPopover.Contains(screen)
        || OverlayEditorUi.Contains(_formatBar, screen)
        || OverlayEditorUi.Contains(_picker.Root, screen)
      )
        return;
      bool popover = AnyPopover;
      ClosePopovers();
      if (!InTextArea(screen))
      {
        if (popover)
          Render();
        return;
      }
      if (_picker.Visible)
        _picker.Hide();
      // A click on a value selects it whole and opens its formula.
      if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && TryValueAt(screen, out int value))
      {
        _lastClick = Time.unscaledTime;
        OpenChip(value);
        return;
      }
      int index = IndexAt(screen);
      bool doubleClick = Time.unscaledTime - _lastClick < .35f;
      _lastClick = Time.unscaledTime;
      if (doubleClick)
      {
        _anchor = _document.WordStart(Mathf.Min(_document.Count, index + 1));
        _caretIndex = _document.WordEnd(_anchor);
        _dragging = false;
        Render();
        return;
      }
      _caretIndex = index;
      if (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
        _anchor = index;
      _dragging = true;
      _goalX = float.NaN;
      Render();
    }
    else if (_dragging && Input.GetMouseButton(0))
    {
      int index = IndexAt(screen);
      if (index != _caretIndex)
      {
        _caretIndex = index;
        Render();
      }
    }
    else if (_dragging)
    {
      _dragging = false;
      Render();
    }
  }

  // The dimmed space around the text belongs to it; the top bar, the format
  // bar, the popovers and the picker don't.
  private bool InTextArea(Vector2 screen)
  {
    foreach (RectTransform blocked in new[] { _dock.Root, _growPopover.Root, _formatBar, _picker.Root })
      if (OverlayEditorUi.Contains(blocked, screen))
        return false;
    float y = _screen.ToFrame(screen).y;
    return y >= OverlayEditorFrame.TopBar && y < _bottomTop - 18;
  }

  private void Type(string text)
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
      TypingSizeFormula(start)
    );
    Changed();
  }

  private void DeleteSelection()
  {
    DeleteRange(Mathf.Min(_anchor, _caretIndex), Mathf.Max(_anchor, _caretIndex));
  }

  private void DeleteRange(int start, int end)
  {
    if (start == end)
      return;
    Remember(false);
    _document.Delete(start, end);
    _caretIndex = _anchor = Mathf.Min(start, end);
    Changed();
  }

  private void MoveHorizontally(int direction, bool extend, bool word, bool line)
  {
    int target;
    if (HasSelection && !extend && !word && !line)
      target = direction < 0 ? Mathf.Min(_anchor, _caretIndex) : Mathf.Max(_anchor, _caretIndex);
    else if (line)
      target = direction < 0 ? LineStart(_caretIndex) : LineEnd(_caretIndex);
    else if (word)
      target = direction < 0 ? _document.WordStart(_caretIndex) : _document.WordEnd(_caretIndex);
    else
      target = _caretIndex + direction;
    MoveTo(target, extend);
  }

  private void MoveTo(int index, bool extend, bool keepGoal = false)
  {
    _caretIndex = _document.Clamp(index);
    if (!extend)
      _anchor = _caretIndex;
    if (!keepGoal)
      _goalX = float.NaN;
    Render();
  }

  private bool Copy()
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
  private void Paste()
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
        TypingSizeFormula(start)
      );
      Changed();
    }
  }

  private void Remember(bool coalesce)
  {
    if (coalesce && _undo.Count > 0)
      return;
    _undo.Add(new Snapshot(_document.Clone(), _caretIndex, _anchor));
    if (_undo.Count > 100)
      _undo.RemoveAt(0);
    _redo.Clear();
    if (!coalesce)
      _lastTypingAt = -1;
  }

  private void Undo()
  {
    if (_undo.Count == 0)
      return;
    _redo.Add(new Snapshot(_document.Clone(), _caretIndex, _anchor));
    Restore(_undo[_undo.Count - 1]);
    _undo.RemoveAt(_undo.Count - 1);
  }

  private void Redo()
  {
    if (_redo.Count == 0)
      return;
    _undo.Add(new Snapshot(_document.Clone(), _caretIndex, _anchor));
    Restore(_redo[_redo.Count - 1]);
    _redo.RemoveAt(_redo.Count - 1);
  }

  private void Restore(Snapshot snapshot)
  {
    _document = snapshot.Document.Clone();
    _caretIndex = snapshot.Caret;
    _anchor = snapshot.Anchor;
    _lastTypingAt = -1;
    _lastFormulaKey = null;
    // The keypad reads the restored formula afresh.
    _editing.Deactivate();
    Render();
  }

  private void Changed()
  {
    _goalX = float.NaN;
    Render();
  }

  // Rebuilds the text at its game size and everything placed around it.
  private void Render()
  {
    if (_screen == null)
      return;
    _blinkStart = Time.unscaledTime;
    _editing.Activate(CurrentSlot());
    // The text stays above the dock.
    SyncDock();
    _bottomTop = _dock.Top;
    // The text must be active before it is laid out for the caret.
    bool empty = _document.Count == 0 && _composition.Length == 0;
    if (_preview.gameObject.activeSelf == empty)
      _preview.gameObject.SetActive(!empty);
    Compose();
    _preview.fontSize = OverlayElementDefaults.TextSize;
    _preview.text = _builder.ToString();
    Vector2 preferred = _preview.GetPreferredValues();
    preferred = new Vector2(Mathf.Max(2, preferred.x), Mathf.Max(_preview.fontSize, preferred.y));
    float frameWidth = _screen.Width;
    float top = OverlayEditorFrame.TopBar + 64,
      bottom = _bottomTop - 40;
    Vector2 available = new Vector2(frameWidth - OverlayEditorFrame.Margin * 2, Mathf.Max(40, bottom - top)) * _scale;
    // The game's size, unless the text would not fit on screen.
    _previewScale = Mathf.Min(_elementScale * ScreenScale, available.x / preferred.x, available.y / preferred.y);
    RectTransform rect = _preview.rectTransform;
    rect.sizeDelta = preferred;
    rect.localScale = new Vector3(_previewScale, _previewScale, 1);
    Vector2 center = new Vector2(frameWidth * .5f, (top + bottom) * .5f) * _scale;
    rect.anchoredPosition = new Vector2(center.x, -center.y);
    _preview.ForceMeshUpdate();
    Map();
    PlaceCaret();
    PlaceDropCaret();
    PlaceMarks();
    float textTop = (center.y - preferred.y * _previewScale * .5f) / _scale;
    ShowFields();
    float barWidth = LayoutFormatBar();
    SettingsUiFactory.Place(
      _formatBar,
      frameWidth * .5f - barWidth * .5f,
      Mathf.Max(OverlayEditorFrame.TopBar, textTop - FormatHeight - 18),
      barWidth,
      FormatHeight
    );
    Color swatch = OverlayTextComposer.Resolve(RangeColor(), OverlayTheme.Accent);
    swatch.a = 1;
    _colorButton.Swatch = swatch;
    PlacePicker();
    RefreshDock();
    PlaceGrowMark();
    PlaceEmptyHint(center, empty);
  }

  // While the grow popover is open, a dot marks the point that stays put.
  private void PlaceGrowMark()
  {
    bool show = _growPopover.Visible;
    _growMark.gameObject.SetActive(show);
    if (!show)
      return;
    Rect text = _screen.FrameRect(_preview.rectTransform);
    Anchor9 point = GrowPointNow();
    Vector2 at = new(
      text.x + text.width * Domain.Layout.OverlayLayoutPosition.AnchorX(point),
      text.y + text.height * Domain.Layout.OverlayLayoutPosition.AnchorY(point)
    );
    SettingsUiFactory.Place(_growMark.rectTransform, at.x - 6, at.y - 6, 12, 12);
    _growMark.transform.SetAsLastSibling();
  }

  // Size, color and grow point side by side; returns the bar's width.
  private float LayoutFormatBar()
  {
    float y = (FormatHeight - OverlayBarButton.Height) * .5f;
    float x = y;
    foreach (OverlayBarButton button in new[] { _sizeButton, _colorButton, _growButton })
    {
      button.Place(x, y);
      x += button.Width + 4;
    }
    return x - 4 + y;
  }

  // An empty text shows a muted hint with the caret just before it, centered
  // where the text will appear, like an empty text field.
  private void PlaceEmptyHint(Vector2 center, bool empty)
  {
    _placeholder.gameObject.SetActive(empty);
    _emptyCaret.gameObject.SetActive(empty);
    if (!empty)
      return;
    float width = Mathf.Ceil(_placeholder.GetPreferredValues(_placeholder.text).x) + 2;
    float x = (center.x / _scale) - width * .5f,
      y = center.y / _scale;
    SettingsUiFactory.Place(_placeholder.rectTransform, x, y - 14, width, 28);
    SettingsUiFactory.Place(_emptyCaret.rectTransform, x - 6, y - 11, 2, 22);
  }

  // The text as the game will draw it, with the editor's examples for values
  // that have no data yet. Each value is a link so its characters can be found.
  private void Compose()
  {
    _builder.Clear();
    Color highlight = OverlayTheme.Accent;
    IOverlayNumberSource samples = _values.WithSamples;
    OverlayTextItem? open = null;
    float openSize = 0;
    for (int i = 0; i <= _document.Count; i++)
    {
      if (i == _caretIndex && _composition.Length > 0)
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize);
        open = null;
        float typing = OverlayTextComposer.Size(
          TypingSize(i),
          OverlayFormulaText.Compile(TypingSizeFormula(i)),
          samples,
          _elementScale
        );
        OverlayTextComposer.OpenStyle(_builder, OverlayTextComposer.Resolve(TypingColor(i), highlight), typing);
        _builder.Append("<u>");
        OverlayTextComposer.AppendLiteral(_builder, _composition);
        _builder.Append("</u>");
        OverlayTextComposer.CloseStyle(_builder, typing);
      }
      if (i == _document.Count)
        break;
      OverlayTextItem item = _document[i];
      if (item.IsValue)
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize);
        open = null;
        float size = ItemSize(item, samples);
        OverlayTextComposer.OpenStyle(_builder, ItemColor(item, highlight, samples), size);
        _builder.Append("<link=\"").Append(OverlayTextComposer.ValueLink).Append("\">");
        int before = _builder.Length;
        OverlayFormulaText.Append(_builder, OverlayFormula.Parse(item.Value), _values, true);
        if (_builder.Length == before)
          _builder.Append(' ');
        _builder.Append("</link>");
        OverlayTextComposer.CloseStyle(_builder, size);
        continue;
      }
      if (open == null || !open.Value.SameStyle(item))
      {
        if (open != null)
          OverlayTextComposer.CloseStyle(_builder, openSize);
        openSize = ItemSize(item, samples);
        OverlayTextComposer.OpenStyle(_builder, ItemColor(item, highlight, samples), openSize);
        open = item;
      }
      OverlayTextComposer.AppendLiteral(_builder, item.Text);
    }
    if (open != null)
      OverlayTextComposer.CloseStyle(_builder, openSize);
  }

  private float ItemSize(OverlayTextItem item, IOverlayNumberSource samples) =>
    OverlayTextComposer.Size(item.Size, OverlayFormulaText.Compile(item.SizeFormula), samples, _elementScale);

  private static Color ItemColor(OverlayTextItem item, Color highlight, IOverlayNumberSource samples) =>
    OverlayTextComposer.Resolve(item.Color, highlight, OverlayFormulaText.Compile(item.Color.AlphaFormula), samples);

  // Where each item's characters are in the laid-out text.
  private void Map()
  {
    _start.Clear();
    _length.Clear();
    _compositionStart = -1;
    TMP_TextInfo info = _preview.textInfo;
    int running = 0,
      link = 0;
    for (int i = 0; i <= _document.Count; i++)
    {
      if (i == _caretIndex && _composition.Length > 0)
      {
        _compositionStart = running;
        running += _composition.Length;
      }
      if (i == _document.Count)
        break;
      if (_document[i].IsValue && link < info.linkCount)
      {
        TMP_LinkInfo value = info.linkInfo[link++];
        _start.Add(value.linkTextfirstCharacterIndex);
        _length.Add(value.linkTextLength);
        running = value.linkTextfirstCharacterIndex + value.linkTextLength;
      }
      else
      {
        _start.Add(running);
        _length.Add(_document[i].IsValue ? 0 : 1);
        running += _document[i].IsValue ? 0 : 1;
      }
    }
  }

  private bool TryCharacter(int index, out TMP_CharacterInfo character)
  {
    TMP_TextInfo info = _preview.textInfo;
    character = default;
    if (index < 0 || index >= info.characterCount)
      return false;
    character = info.characterInfo[index];
    return true;
  }

  private bool IsLineBreak(int item) =>
    item >= 0 && item < _document.Count && !_document[item].IsValue && _document[item].Text == "\n";

  // The caret's x, and the line it stands on, in the text's local space.
  private void CaretGeometry(int index, out float x, out int line)
  {
    TMP_TextInfo info = _preview.textInfo;
    line = 0;
    x = HorizontalStart(0);
    if (
      _compositionStart >= 0
      && index == _caretIndex
      && TryCharacter(_compositionStart + _composition.Length - 1, out TMP_CharacterInfo composed)
    )
    {
      x = composed.xAdvance;
      line = composed.lineNumber;
      return;
    }
    if (index < _document.Count && _length[index] > 0 && TryCharacter(_start[index], out TMP_CharacterInfo next))
    {
      x = next.origin;
      line = next.lineNumber;
      return;
    }
    for (int previous = index - 1; previous >= 0; previous--)
    {
      if (_length[previous] == 0 || !TryCharacter(_start[previous] + _length[previous] - 1, out TMP_CharacterInfo last))
        continue;
      if (IsLineBreak(previous))
      {
        line = last.lineNumber + 1;
        x = HorizontalStart(line);
      }
      else
      {
        x = last.xAdvance;
        line = last.lineNumber;
      }
      return;
    }
    if (info.lineCount > 0)
      x = HorizontalStart(0);
  }

  // Where an empty line's caret sits for the text's alignment.
  private float HorizontalStart(int line)
  {
    Rect rect = _preview.rectTransform.rect;
    TMP_TextInfo info = _preview.textInfo;
    if (
      line < info.lineCount
      && info.lineInfo[line].characterCount > 0
      && TryCharacter(info.lineInfo[line].firstCharacterIndex, out TMP_CharacterInfo first)
      && first.character != '\n'
    )
      return first.origin;
    return _preview.alignment switch
    {
      TextAlignmentOptions.MidlineLeft => rect.xMin,
      TextAlignmentOptions.MidlineRight => rect.xMax,
      _ => rect.center.x,
    };
  }

  private void LineExtent(int line, out float top, out float bottom)
  {
    TMP_TextInfo info = _preview.textInfo;
    float height = _preview.fontSize * 1.2f;
    if (info.lineCount == 0)
    {
      top = height * .5f;
      bottom = -height * .5f;
      return;
    }
    int known = Mathf.Clamp(line, 0, info.lineCount - 1);
    TMP_LineInfo lineInfo = info.lineInfo[known];
    float shift = (line - known) * Mathf.Max(1, lineInfo.lineHeight > 0 ? lineInfo.lineHeight : height);
    top = lineInfo.ascender - shift;
    bottom = lineInfo.descender - shift;
  }

  // The caret stays two screen pixels wide at any text size.
  private void PlaceCaret()
  {
    CaretGeometry(_caretIndex, out float x, out int line);
    LineExtent(line, out float top, out float bottom);
    float width = 2 / Mathf.Max(.01f, _previewScale);
    RectTransform caret = _caret.rectTransform;
    caret.anchoredPosition = new Vector2(x - width * .5f, bottom);
    caret.sizeDelta = new Vector2(width, top - bottom);
    _caret.color = Color.white;
    _caret.transform.SetAsLastSibling();
  }

  // Selection behind the text, and a soft pill behind every value.
  private void PlaceMarks()
  {
    int used = 0;
    int start = Mathf.Min(_anchor, _caretIndex),
      end = Mathf.Max(_anchor, _caretIndex);
    Color selection = SettingsPalette.SoftAccent;
    selection.a = .38f;
    for (int i = start; i < end; i++)
      used = AddItemRect(_selectionRects, used, i, selection, 0);
    Hide(_selectionRects, used);
    used = 0;
    float padding = 3 / Mathf.Max(.01f, _previewScale);
    // The value the keypad edits glows in the accent; a value waiting for a
    // dropped value does too while one is carried over it.
    Color active = SettingsPalette.SoftAccent;
    active.a = _dragValue != null && _dropTarget == DropTarget.Join ? .55f : .3f;
    int editing = ChipOpen(out int selected) ? selected : -1;
    for (int i = 0; i < _document.Count; i++)
      if (_document[i].IsValue)
        used = AddItemRect(_pillRects, used, i, i == editing ? active : new Color(1, 1, 1, .12f), padding);
    Hide(_pillRects, used);
    foreach (Image pill in _pillRects)
      pill.transform.SetAsFirstSibling();
    _caret.transform.SetAsLastSibling();
  }

  private int AddItemRect(List<Image> pool, int used, int item, Color color, float padding)
  {
    if (_length[item] == 0 || !TryCharacter(_start[item], out TMP_CharacterInfo first))
      return used;
    TryCharacter(_start[item] + _length[item] - 1, out TMP_CharacterInfo last);
    LineExtent(first.lineNumber, out float top, out float bottom);
    float left = first.origin,
      right = IsLineBreak(item) ? first.origin + _preview.fontSize * .3f : Mathf.Max(last.xAdvance, first.origin + 2);
    if (used >= pool.Count)
    {
      Image image = _ui.Panel("Mark", _preview.rectTransform, 0, 0, 1, 1, color, false);
      Anchor(image.rectTransform);
      pool.Add(image);
    }
    Image rect = pool[used];
    rect.gameObject.SetActive(true);
    rect.color = color;
    rect.rectTransform.anchoredPosition = new Vector2(left - padding, bottom - padding * .5f);
    rect.rectTransform.sizeDelta = new Vector2(right - left + padding * 2, top - bottom + padding);
    return used + 1;
  }

  private static void Hide(List<Image> pool, int used)
  {
    for (int i = used; i < pool.Count; i++)
      if (pool[i].gameObject.activeSelf)
        pool[i].gameObject.SetActive(false);
  }

  // The item before which a click at this screen point puts the caret.
  private int IndexAt(Vector2 screen)
  {
    if (_document.Count == 0)
      return 0;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(_preview.rectTransform, screen, null, out Vector2 local);
    return IndexAtLocal(local);
  }

  private int IndexAtLocal(Vector2 local)
  {
    TMP_TextInfo info = _preview.textInfo;
    int line = 0;
    float best = float.PositiveInfinity;
    int lines = info.lineCount + (IsLineBreak(_document.Count - 1) ? 1 : 0);
    for (int l = 0; l < lines; l++)
    {
      LineExtent(l, out float top, out float bottom);
      float distance =
        local.y > top ? local.y - top
        : local.y < bottom ? bottom - local.y
        : 0;
      if (distance < best)
      {
        best = distance;
        line = l;
      }
    }
    int after = -1;
    for (int i = 0; i < _document.Count; i++)
    {
      if (_length[i] == 0 || !TryCharacter(_start[i], out TMP_CharacterInfo first))
        continue;
      if (first.lineNumber < line)
        continue;
      if (first.lineNumber > line)
        return after >= 0 ? after : i;
      if (IsLineBreak(i))
        return i;
      TryCharacter(_start[i] + _length[i] - 1, out TMP_CharacterInfo last);
      if (local.x < (first.origin + last.xAdvance) * .5f)
        return i;
      after = i + 1;
    }
    return after >= 0 ? after : _document.Count;
  }

  private int LineStep(int direction)
  {
    CaretGeometry(_caretIndex, out float x, out int line);
    if (float.IsNaN(_goalX))
      _goalX = x;
    int target = line + direction;
    if (target < 0)
      return 0;
    TMP_TextInfo info = _preview.textInfo;
    int lines = info.lineCount + (IsLineBreak(_document.Count - 1) ? 1 : 0);
    if (target >= lines)
      return _document.Count;
    LineExtent(target, out float top, out float bottom);
    return IndexAtLocal(new Vector2(_goalX, (top + bottom) * .5f));
  }

  private int LineStart(int index)
  {
    CaretGeometry(index, out _, out int line);
    for (int i = 0; i < _document.Count; i++)
      if (LineOf(i) >= line)
        return i;
    return _document.Count;
  }

  private int LineEnd(int index)
  {
    CaretGeometry(index, out _, out int line);
    for (int i = 0; i < _document.Count; i++)
    {
      int itemLine = LineOf(i);
      if (itemLine > line || (itemLine == line && IsLineBreak(i)))
        return i;
    }
    return _document.Count;
  }

  private int LineOf(int item) =>
    _length[item] > 0 && TryCharacter(_start[item], out TMP_CharacterInfo character) ? character.lineNumber : 0;

  private void UpdateImePosition()
  {
    CaretGeometry(_caretIndex, out float x, out int line);
    LineExtent(line, out _, out float bottom);
    Vector2 screen = RectTransformUtility.WorldToScreenPoint(
      null,
      _preview.rectTransform.TransformPoint(new Vector3(x, bottom, 0))
    );
    Input.compositionCursorPos = new Vector2(screen.x, Screen.height - screen.y);
  }

  private readonly struct Snapshot
  {
    public readonly OverlayTextDocument Document;
    public readonly int Caret;
    public readonly int Anchor;

    public Snapshot(OverlayTextDocument document, int caret, int anchor)
    {
      Document = document;
      Caret = caret;
      Anchor = anchor;
    }
  }
}
