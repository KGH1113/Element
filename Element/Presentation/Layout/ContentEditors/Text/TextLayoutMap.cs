using System.Collections.Generic;
using Element.Domain.Elements;
using Element.Presentation.Layout.ContentEditors.Shell;
using TMPro;
using UnityEngine;

namespace Element.Presentation.Layout.ContentEditors.Text;

/// <summary>
/// Where each item of the text is in the laid-out preview: its characters,
/// where the caret stands, each line's extent, and the item under a point.
/// Built afresh after each layout.
/// </summary>
internal sealed class TextLayoutMap
{
  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private readonly List<int> _start = new();
  private readonly List<int> _length = new();
  private int _compositionStart = -1;

  public TextLayoutMap(TextDocumentEditing text, TextPreview preview)
  {
    _text = text;
    _preview = preview;
  }

  private TextMeshProUGUI Preview => _preview.Text;

  // Where each item's characters are in the laid-out text.
  public void Map()
  {
    _start.Clear();
    _length.Clear();
    _compositionStart = -1;
    OverlayTextDocument document = _text.Document;
    string composition = _text.Composition;
    TMP_TextInfo info = Preview.textInfo;
    int running = 0,
      link = 0;
    for (int i = 0; i <= document.Count; i++)
    {
      if (i == _text.Caret && composition.Length > 0)
      {
        _compositionStart = running;
        running += composition.Length;
      }
      if (i == document.Count)
        break;
      if (document[i].IsValue && link < info.linkCount)
      {
        TMP_LinkInfo value = info.linkInfo[link++];
        _start.Add(value.linkTextfirstCharacterIndex);
        _length.Add(value.linkTextLength);
        running = value.linkTextfirstCharacterIndex + value.linkTextLength;
      }
      else
      {
        _start.Add(running);
        _length.Add(document[i].IsValue ? 0 : 1);
        running += document[i].IsValue ? 0 : 1;
      }
    }
  }

  public bool TryCharacter(int index, out TMP_CharacterInfo character)
  {
    TMP_TextInfo info = Preview.textInfo;
    character = default;
    if (index < 0 || index >= info.characterCount)
      return false;
    character = info.characterInfo[index];
    return true;
  }

  /// <summary>The first and last characters of <paramref name="item"/>; false when it shows none.</summary>
  public bool ItemCharacters(int item, out TMP_CharacterInfo first, out TMP_CharacterInfo last)
  {
    last = default;
    if (_length[item] == 0 || !TryCharacter(_start[item], out first))
    {
      first = default;
      return false;
    }
    TryCharacter(_start[item] + _length[item] - 1, out last);
    return true;
  }

  public bool IsLineBreak(int item)
  {
    OverlayTextDocument document = _text.Document;
    return item >= 0 && item < document.Count && !document[item].IsValue && document[item].Text == "\n";
  }

  /// <summary>The lines of the text, with one more after a line break at its end.</summary>
  public int Lines => Preview.textInfo.lineCount + (IsLineBreak(_text.Document.Count - 1) ? 1 : 0);

  // The caret's x, and the line it stands on, in the text's local space.
  public void CaretGeometry(int index, out float x, out int line)
  {
    TMP_TextInfo info = Preview.textInfo;
    line = 0;
    x = HorizontalStart(0);
    string composition = _text.Composition;
    if (
      _compositionStart >= 0
      && index == _text.Caret
      && TryCharacter(_compositionStart + composition.Length - 1, out TMP_CharacterInfo composed)
    )
    {
      x = composed.xAdvance;
      line = composed.lineNumber;
      return;
    }
    if (index < _text.Document.Count && _length[index] > 0 && TryCharacter(_start[index], out TMP_CharacterInfo next))
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
    Rect rect = Preview.rectTransform.rect;
    TMP_TextInfo info = Preview.textInfo;
    if (
      line < info.lineCount
      && info.lineInfo[line].characterCount > 0
      && TryCharacter(info.lineInfo[line].firstCharacterIndex, out TMP_CharacterInfo first)
      && first.character != '\n'
    )
      return first.origin;
    return Preview.alignment switch
    {
      TextAlignmentOptions.MidlineLeft => rect.xMin,
      TextAlignmentOptions.MidlineRight => rect.xMax,
      _ => rect.center.x,
    };
  }

  public void LineExtent(int line, out float top, out float bottom)
  {
    TMP_TextInfo info = Preview.textInfo;
    float height = Preview.fontSize * 1.2f;
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

  // The item before which a click at this screen point puts the caret.
  public int IndexAt(Vector2 screen)
  {
    if (_text.Document.Count == 0)
      return 0;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(Preview.rectTransform, screen, null, out Vector2 local);
    return IndexAtLocal(local);
  }

  public int IndexAtLocal(Vector2 local)
  {
    int count = _text.Document.Count;
    int line = 0;
    float best = float.PositiveInfinity;
    int lines = Lines;
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
    for (int i = 0; i < count; i++)
    {
      if (!ItemCharacters(i, out TMP_CharacterInfo first, out TMP_CharacterInfo last))
        continue;
      if (first.lineNumber < line)
        continue;
      if (first.lineNumber > line)
        return after >= 0 ? after : i;
      if (IsLineBreak(i))
        return i;
      if (local.x < (first.origin + last.xAdvance) * .5f)
        return i;
      after = i + 1;
    }
    return after >= 0 ? after : count;
  }

  public int LineStart(int index)
  {
    CaretGeometry(index, out _, out int line);
    int count = _text.Document.Count;
    for (int i = 0; i < count; i++)
      if (LineOf(i) >= line)
        return i;
    return count;
  }

  public int LineEnd(int index)
  {
    CaretGeometry(index, out _, out int line);
    int count = _text.Document.Count;
    for (int i = 0; i < count; i++)
    {
      int itemLine = LineOf(i);
      if (itemLine > line || (itemLine == line && IsLineBreak(i)))
        return i;
    }
    return count;
  }

  private int LineOf(int item) =>
    _length[item] > 0 && TryCharacter(_start[item], out TMP_CharacterInfo character) ? character.lineNumber : 0;

  // ---- Values under the pointer ----

  /// <summary>The value under a screen point.</summary>
  public bool TryValueAt(Vector2 screen, out int index)
  {
    OverlayTextDocument document = _text.Document;
    for (index = 0; index < document.Count; index++)
      if (document[index].IsValue && ItemContains(index, screen))
        return true;
    index = -1;
    return false;
  }

  /// <summary>Whether a screen point is over the item, a little wider than its characters.</summary>
  public bool ItemContains(int item, Vector2 screen)
  {
    if (!ItemRect(item, out Rect local))
      return false;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(Preview.rectTransform, screen, null, out Vector2 point);
    float padding = 3 / Mathf.Max(.01f, _preview.Scale);
    local.xMin -= padding;
    local.xMax += padding;
    return local.Contains(point);
  }

  // The item's top center in frame units.
  public bool ItemTop(int item, OverlayEditorFrame screen, out Vector2 anchor)
  {
    anchor = default;
    if (!ItemRect(item, out Rect local))
      return false;
    Vector3 world = Preview.rectTransform.TransformPoint(new Vector3(local.center.x, local.yMax, 0));
    anchor = screen.ToFrame(RectTransformUtility.WorldToScreenPoint(null, world));
    return true;
  }

  /// <summary>The item's box in the text's local space (y up).</summary>
  public bool ItemRect(int item, out Rect rect)
  {
    rect = default;
    if (
      item < 0
      || item >= _length.Count
      || !ItemCharacters(item, out TMP_CharacterInfo first, out TMP_CharacterInfo last)
    )
      return false;
    LineExtent(first.lineNumber, out float top, out float bottom);
    rect = Rect.MinMaxRect(first.origin, bottom, Mathf.Max(last.xAdvance, first.origin + 2), top);
    return true;
  }
}
