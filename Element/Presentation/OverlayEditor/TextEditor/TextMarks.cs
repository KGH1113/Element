using System.Collections.Generic;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// What the editor draws on the text besides its pieces' underlines
/// (<see cref="TextUnderlines"/>): the caret, the selection behind the
/// characters, and for an empty text the caret alone where it will start.
/// </summary>
internal sealed class TextMarks
{
  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private readonly TextLayoutMap _map;
  private readonly List<Image> _selectionRects = new();
  private UiFactory _ui;
  private Image _caret;
  private Image _emptyCaret;

  public TextMarks(TextDocumentEditing text, TextPreview preview, TextLayoutMap map)
  {
    _text = text;
    _preview = preview;
    _map = map;
  }

  /// <summary>The carets on the preview, built after it.</summary>
  public void BuildCarets(UiFactory ui)
  {
    _ui = ui;
    _selectionRects.Clear();
    RectTransform preview = _preview.Text.rectTransform;
    _caret = ui.Panel("Caret", preview, 0, 0, 2, 10, UiPalette.SoftAccent, false);
    Anchor(_caret.rectTransform);
  }

  /// <summary>The empty text's caret, over the controls: where the first letter will go.</summary>
  public void BuildHint(UiFactory ui, RectTransform frame) =>
    _emptyCaret = ui.Panel("Empty caret", frame, 0, 0, 2, 22, UiPalette.SoftAccent, false);

  /// <summary>Shows or hides both carets, as they blink.</summary>
  public void Blink(bool visible)
  {
    _caret.enabled = visible;
    _emptyCaret.enabled = _caret.enabled;
  }

  // The caret stays two screen pixels wide at any text size.
  public void PlaceCaret()
  {
    _map.CaretGeometry(_text.Caret, out float x, out int line);
    _map.LineExtent(line, out float top, out float bottom);
    float width = 2 / Mathf.Max(.01f, _preview.Scale);
    RectTransform caret = _caret.rectTransform;
    caret.anchoredPosition = new Vector2(x - width * .5f, bottom);
    caret.sizeDelta = new Vector2(width, top - bottom);
    _caret.color = UiPalette.SoftAccent;
    _caret.transform.SetAsLastSibling();
  }

  /// <summary>The selection, behind the characters.</summary>
  public void Place()
  {
    int used = 0;
    int start = Mathf.Min(_text.Anchor, _text.Caret),
      end = Mathf.Max(_text.Anchor, _text.Caret);
    Color selection = UiPalette.SoftAccent;
    selection.a = .38f;
    for (int i = start; i < end; i++)
      used = AddItemRect(_selectionRects, used, i, selection, 0);
    Hide(_selectionRects, used);
    _caret.transform.SetAsLastSibling();
  }

  private int AddItemRect(List<Image> pool, int used, int item, Color color, float padding)
  {
    if (!_map.ItemCharacters(item, out TMP_CharacterInfo first, out TMP_CharacterInfo last))
      return used;
    _map.LineExtent(first.lineNumber, out float top, out float bottom);
    float left = first.origin,
      right = _map.IsLineBreak(item)
        ? first.origin + _preview.Text.fontSize * .3f
        : Mathf.Max(last.xAdvance, first.origin + 2);
    if (used >= pool.Count)
    {
      Image image = _ui.Panel("Mark", _preview.Text.rectTransform, 0, 0, 1, 1, color, false);
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

  // An empty text shows only its caret, where the text will appear.
  public void PlaceEmptyHint(Vector2 center, bool empty, float frameScale)
  {
    _emptyCaret.gameObject.SetActive(empty);
    if (empty)
      UiFactory.Place(_emptyCaret.rectTransform, center.x / frameScale - 1, center.y / frameScale - 11, 2, 22);
  }

  private static void Anchor(RectTransform rect)
  {
    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
    rect.pivot = Vector2.zero;
  }
}
