using System.Collections.Generic;
using Element.Presentation.Ui.Theme;
using Element.Presentation.Ui.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Element.Presentation.Ui.Theme.UiLocale;

namespace Element.Presentation.OverlayEditor.TextEditor;

/// <summary>
/// What the editor draws on the text: the caret, an accent caret where a
/// dragged value would land, the selection behind the characters, a soft
/// pill behind every value, and for an empty text a muted hint with the
/// caret just before it, like an empty text field.
/// </summary>
internal sealed class TextMarks
{
  private readonly TextDocumentEditing _text;
  private readonly TextPreview _preview;
  private readonly TextLayoutMap _map;
  private readonly List<Image> _selectionRects = new();
  private readonly List<Image> _pillRects = new();
  private UiFactory _ui;
  private Image _caret;
  private Image _dropCaret;
  private TextMeshProUGUI _placeholder;
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
    _pillRects.Clear();
    RectTransform preview = _preview.Text.rectTransform;
    _caret = ui.Panel("Caret", preview, 0, 0, 2, 10, Color.white, false);
    Anchor(_caret.rectTransform);
    _dropCaret = ui.Panel("Drop caret", preview, 0, 0, 3, 10, UiPalette.SoftAccent, false);
    Anchor(_dropCaret.rectTransform);
    _dropCaret.enabled = false;
  }

  /// <summary>The empty text's hint and its caret, over the controls.</summary>
  public void BuildHint(UiFactory ui, RectTransform frame)
  {
    _placeholder = ui.Text(
      L("텍스트를 입력하세요", "Type your text"),
      frame,
      0,
      0,
      10,
      28,
      16,
      new Color(1, 1, 1, .38f)
    );
    _placeholder.alignment = TextAlignmentOptions.MidlineLeft;
    _placeholder.textWrappingMode = TextWrappingModes.NoWrap;
    // A caret in front of the hint, like an empty text field.
    _emptyCaret = ui.Panel("Empty caret", frame, 0, 0, 2, 22, Color.white, false);
  }

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
    _caret.color = Color.white;
    _caret.transform.SetAsLastSibling();
  }

  /// <summary>
  /// The drop caret before item <paramref name="dropIndex"/> (none for -1),
  /// then the selection and the values' pills. The value at
  /// <paramref name="editing"/> glows; brighter while a value is carried over
  /// it to <paramref name="joining"/> its formula.
  /// </summary>
  public void Place(int dropIndex, bool joining, int editing)
  {
    PlaceDropCaret(dropIndex);
    PlaceMarks(joining, editing);
  }

  // An accent caret where a dragged value would be inserted.
  private void PlaceDropCaret(int index)
  {
    bool show = index >= 0;
    _dropCaret.enabled = show;
    if (!show)
      return;
    _map.CaretGeometry(index, out float x, out int line);
    _map.LineExtent(line, out float top, out float bottom);
    float width = 3 / Mathf.Max(.01f, _preview.Scale);
    RectTransform caret = _dropCaret.rectTransform;
    caret.anchoredPosition = new Vector2(x - width * .5f, bottom);
    caret.sizeDelta = new Vector2(width, top - bottom);
    _dropCaret.color = UiPalette.SoftAccent;
    _dropCaret.transform.SetAsLastSibling();
  }

  // Selection behind the text, and a soft pill behind every value.
  private void PlaceMarks(bool joining, int editing)
  {
    int used = 0;
    int start = Mathf.Min(_text.Anchor, _text.Caret),
      end = Mathf.Max(_text.Anchor, _text.Caret);
    Color selection = UiPalette.SoftAccent;
    selection.a = .38f;
    for (int i = start; i < end; i++)
      used = AddItemRect(_selectionRects, used, i, selection, 0);
    Hide(_selectionRects, used);
    used = 0;
    float padding = 3 / Mathf.Max(.01f, _preview.Scale);
    // The value the keypad edits glows in the accent; a value waiting for a
    // dropped value does too while one is carried over it.
    Color active = UiPalette.SoftAccent;
    active.a = joining ? .55f : .3f;
    int count = _text.Document.Count;
    for (int i = 0; i < count; i++)
      if (_text.Document[i].IsValue)
        used = AddItemRect(_pillRects, used, i, i == editing ? active : new Color(1, 1, 1, .12f), padding);
    Hide(_pillRects, used);
    foreach (Image pill in _pillRects)
      pill.transform.SetAsFirstSibling();
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

  // An empty text shows a muted hint with the caret just before it, centered
  // where the text will appear, like an empty text field.
  public void PlaceEmptyHint(Vector2 center, bool empty, float frameScale)
  {
    _placeholder.gameObject.SetActive(empty);
    _emptyCaret.gameObject.SetActive(empty);
    if (!empty)
      return;
    float width = Mathf.Ceil(_placeholder.GetPreferredValues(_placeholder.text).x) + 2;
    float x = (center.x / frameScale) - width * .5f,
      y = center.y / frameScale;
    UiFactory.Place(_placeholder.rectTransform, x, y - 14, width, 28);
    UiFactory.Place(_emptyCaret.rectTransform, x - 6, y - 11, 2, 22);
  }

  private static void Anchor(RectTransform rect)
  {
    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
    rect.pivot = Vector2.zero;
  }
}
